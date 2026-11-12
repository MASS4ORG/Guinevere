using SkiaSharp;

namespace Guinevere.Profiling;

/// <summary>Gates a host before callbacks, time and input; cached images never invoke target drawing again.</summary>
public sealed class GuiFrameController : IGuiFrameController, IDisposable
{
    Gui? _gui;
    Gui TargetGui => _gui ?? throw new ObjectDisposedException(nameof(GuiFrameController));
    readonly Queue<RecordedInputFrame> _pending = [];
    readonly List<RecordedInputFrame> _recorded = [];
    readonly List<GuiInputEvent> _events = [];
    RecordedInputHandler? _input;
    InputRecording? _playback;
    InputScriptRecording? _script;
    SKImage? _image;
    long _pendingBytes, _recordedBytes, _eventBytes;
    double _pendingTime, _stepDelta = 1d / 60;
    int _steps, _playhead;
    int _deliveredPlaybackIndex;
    bool _disposed, _paused, _loopResetPending;

    /// <summary>Attaches an exclusive gate; custom clocks require a host-specific controller.</summary>
    public GuiFrameController(Gui gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        if (gui.FrameController is not null || !ReferenceEquals(gui.Clock, gui.Time))
        {
            throw new InvalidOperationException("A controller or custom clock is already installed.");
        }
        _gui = gui;
        gui.FrameController = this;
    }

    /// <summary>Whether target execution is stopped while host presentation remains live.</summary>
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            _pendingTime = 0;
            _steps = 0;
            TargetGui.RequestFrame();
        }
    }
    /// <summary>Whether delivered target frames are appended to the bounded recording.</summary>
    public bool Recording { get; set; }
    /// <summary>Whether playback restarts after the final recorded frame.</summary>
    public bool Loop { get; set; }
    /// <summary>Application-owned reset invoked before playback and each loop; required for deterministic loops.</summary>
    public Action? ResetTarget { get; set; }
    /// <summary>Virtual target-time multiplier, validated when a frame is executed.</summary>
    public double TimeScale { get; set; } = 1;
    /// <summary>Maximum queued paused frames and recorded delivered frames.</summary>
    public int MaximumFrames { get; init; } = 600;
    /// <summary>Maximum bytes for each input queue or recording.</summary>
    public long MaximumBytes { get; init; } = 32 * 1024 * 1024;
    /// <summary>Maximum pixels in a cached target presentation.</summary>
    public long MaximumSurfacePixels { get; init; } = 16 * 1024 * 1024;
    /// <summary>Visible count of input frames dropped because of a retention limit.</summary>
    public long DroppedInputFrames { get; private set; }
    /// <summary>Visible count of native input events dropped before a boundary.</summary>
    public long DroppedInputEvents { get; private set; }
    /// <summary>Copied latest target error; no exception or target type is retained.</summary>
    public string? LastError { get; private set; }
    /// <summary>Whether a recording is currently being replayed.</summary>
    public bool Playing => _playback is not null;

    /// <summary>Number of failed legacy checkpoints during the current playback.</summary>
    public int ScriptFailures { get; private set; }
    /// <summary>Latest copied legacy checkpoint result.</summary>
    public string? PlaybackMessage { get; private set; }
    /// <summary>Optional directory for legacy dump commands.</summary>
    public string? ScriptOutputDirectory { get; set; }

    /// <summary>Requests exactly one complete target frame using the supplied virtual delta.</summary>
    public void Step(double deltaSeconds = 1d / 60)
    {
        ValidateTime(deltaSeconds);
        _paused = true;
        _stepDelta = deltaSeconds;
        _steps++;
        TargetGui.RequestFrame();
    }

    /// <inheritdoc />
    public void UpdateTime(double deltaSeconds)
    {
        ValidateTime(deltaSeconds);
        if (!_paused)
        {
            _pendingTime += deltaSeconds;
        }
    }

    /// <inheritdoc />
    public void Input(in GuiInputEvent input)
    {
        var bytes = 96L + (input.Text?.Length ?? 0) * 2L;
        if (_events.Count < 16384 && (input.Text?.Length ?? 0) <= 16384 && bytes <= MaximumBytes - _eventBytes)
        {
            _events.Add(input);
            _eventBytes += bytes;
        }
        else
        {
            DroppedInputEvents++;
        }
    }

    /// <summary>Copies the bounded delivered recording for save or playback.</summary>
    public InputRecording GetRecording() => new(_recorded);

    /// <summary>Clears recorded input without resetting arbitrary application state.</summary>
    public void ClearRecording()
    {
        _recorded.Clear();
        _recordedBytes = 0;
    }

    /// <summary>Starts frame-preserving playback, optionally restoring application-owned state first.</summary>
    public void Play(InputRecording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        if (recording.Frames.Count == 0)
        {
            throw new ArgumentException("Recording is empty.", nameof(recording));
        }
        if (Loop && ResetTarget is null)
        {
            throw new InvalidOperationException("Loop playback requires an explicit target reset.");
        }
        ResetTarget?.Invoke();
        _script = null;
        ScriptFailures = 0;
        PlaybackMessage = null;
        _playback = recording;
        _playhead = 0;
        _loopResetPending = false;
        _pending.Clear();
        _pendingBytes = 0;
        TargetGui.RequestFrame();
    }

    /// <summary>Stops playback without changing target state.</summary>
    public void Stop() => _playback = null;

    /// <summary>Queues the frozen target image into another GUI without executing target callbacks.</summary>
    public void DrawPreview(Gui viewer, Rect destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_image is not null)
        {
            viewer.DrawImage(_image, destination);
        }
    }

    /// <summary>Plays an existing InputScript with its original frame boundaries and post-frame checks.</summary>
    public void Play(InputScript script)
    {
        var compiled = InputScriptRecording.Compile(script, MaximumFrames, MaximumBytes);
        Play(compiled.Recording);
        _script = compiled;
    }

    /// <inheritdoc />
    public void RenderFrame(SKCanvas canvas, Action draw)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateSettings();
        var frame = CaptureInput(canvas);
        Enqueue(frame);
        if (!_paused || _steps > 0)
        {
            Deliver(canvas, draw, frame);
        }
        var bounds = canvas.LocalClipBounds;
        if (_image is not null)
        {
            canvas.DrawImage(_image, new SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom),
                new SKSamplingOptions(SKFilterMode.Linear));
        }
        if (_playback is not null && !_paused)
        {
            TargetGui.RequestFrame();
        }
    }

    void ValidateSettings()
    {
        if (!double.IsFinite(TimeScale) || TimeScale <= 0 || MaximumFrames <= 0 || MaximumBytes <= 0)
        {
            throw new InvalidOperationException("Invalid playback speed or input retention limits.");
        }
    }

    RecordedInputFrame CaptureInput(SKCanvas canvas)
    {
        var raw = TargetGui.PlatformInput;
        var game = TargetGui.Platform.TryGet<IGameInputCapability>(out var gameInput) ? gameInput : null;
        var touch = TargetGui.Platform.TryGet<ITouchCapability>(out var touchInput) ? touchInput : null;
        var bounds = canvas.LocalClipBounds;
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        var scale = TargetGui.Platform.TryGet<IDisplayCapability>(out var display) ? display!.ScaleFactor : 1;
        var frame = InputFrameRecorder.Capture(raw, _pendingTime, width, height, scale, _events, game, touch);
        _events.Clear();
        _eventBytes = 0;
        _pendingTime = 0;
        return frame;
    }

    void Deliver(SKCanvas canvas, Action draw, RecordedInputFrame frame)
    {
        var raw = TargetGui.PlatformInput;
        TargetGui.Platform.TryGet<IGameInputCapability>(out var game);
        TargetGui.Platform.TryGet<ITouchCapability>(out var touch);
        var stepping = _paused;
        if (stepping)
        {
            _steps--;
        }
        try
        {
            var delivered = NextFrame(frame);
            Execute(canvas, draw, delivered, stepping ? _stepDelta : delivered.DeltaSeconds, raw, game, touch);
        }
        catch (Exception error)
        {
            LastError = error.ToString();
            _paused = true;
            _steps = 0;
        }
    }

    void Enqueue(RecordedInputFrame frame)
    {
        if (_playback is not null)
        {
            return;
        }
        var bytes = frame.RetainedBytes;
        if (_pending.Count >= MaximumFrames || bytes > MaximumBytes - _pendingBytes)
        {
            DroppedInputFrames++;
            return;
        }
        _pending.Enqueue(frame);
        _pendingBytes += bytes;
    }

    RecordedInputFrame NextFrame(RecordedInputFrame fallback)
    {
        if (_playback is { } playback)
        {
            if (_loopResetPending)
            {
                if (ResetTarget is null)
                {
                    throw new InvalidOperationException("Loop playback requires an explicit target reset.");
                }
                ResetTarget();
                _loopResetPending = false;
            }
            _deliveredPlaybackIndex = _playhead;
            var frame = playback.Frames[_playhead++];
            if (_playhead == playback.Frames.Count)
            {
                if (Loop)
                {
                    _playhead = 0;
                    _loopResetPending = true;
                }
                else
                {
                    _playback = null;
                }
            }
            return frame;
        }
        _script = null;
        if (_pending.TryDequeue(out var pending))
        {
            _pendingBytes -= pending.RetainedBytes;
            return pending;
        }
        _script = null;
        return fallback;
    }

    void Execute(SKCanvas canvas, Action draw, RecordedInputFrame frame, double delta, IInputHandler raw,
        IGameInputCapability? game, ITouchCapability? touch)
    {
        _input ??= new RecordedInputHandler(raw);
        _input.SetFrame(frame);
        TargetGui.Input = _input;
        TargetGui.Platform.Register<IGameInputCapability>(_input);
        TargetGui.Platform.Register<ITouchCapability>(_input);
        try
        {
            TargetGui.Time.Update(delta * TimeScale);
            TargetGui.ExecuteFrame(canvas, draw);
            var image = CapturePresentation(canvas);
            _image?.Dispose();
            _image = image;
            if (_script?.Check(_deliveredPlaybackIndex, TargetGui, image, ScriptOutputDirectory) is { } result)
            {
                PlaybackMessage = result.Message;
                if (!result.Success)
                {
                    ScriptFailures++;
                }
            }
            LastError = null;
            if (Recording)
            {
                Record(frame.WithDelta(delta * TimeScale));
            }
        }
        catch (Exception error)
        {
            LastError = error.ToString();
            _paused = true;
            _steps = 0;
        }
        finally
        {
            TargetGui.Input = raw;
            RestoreCapability(game);
            RestoreCapability(touch);
        }
    }

    SKImage CapturePresentation(SKCanvas canvas)
    {
        var bounds = canvas.DeviceClipBounds;
        if ((long)bounds.Width * bounds.Height > MaximumSurfacePixels)
        {
            throw new InvalidOperationException("Target presentation exceeds the configured pixel budget.");
        }
        var surface = canvas.Surface
            ?? throw new NotSupportedException("The host must supply a surface-backed canvas for frozen presentation.");
        var image = surface.Snapshot(bounds)
            ?? throw new InvalidOperationException("Unable to capture the target presentation.");
        if (!image.IsTextureBacked)
        {
            return image;
        }
        try
        {
            return image.ToRasterImage(true)
                ?? throw new InvalidOperationException("Unable to read back the target presentation.");
        }
        finally
        {
            image.Dispose();
        }
    }

    void Record(RecordedInputFrame frame)
    {
        var bytes = frame.RetainedBytes;
        if (_recorded.Count >= MaximumFrames || bytes > MaximumBytes - _recordedBytes)
        {
            DroppedInputFrames++;
            Recording = false;
            return;
        }
        _recorded.Add(frame);
        _recordedBytes += bytes;
    }

    void RestoreCapability<T>(T? capability) where T : class, IPlatformCapability
    {
        if (capability is null)
        {
            TargetGui.Platform.Remove<T>();
        }
        else
        {
            TargetGui.Platform.Register(capability);
        }
    }

    static void ValidateTime(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }
    }

    /// <summary>Restores host routing and releases cached native images and application reset callbacks.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        if (ReferenceEquals(TargetGui.FrameController, this))
        {
            TargetGui.FrameController = null;
        }
        ResetTarget = null;
        _input = null;
        _pending.Clear();
        _recorded.Clear();
        _events.Clear();
        _playback = null;
        _image?.Dispose();
        _disposed = true;
        _gui = null;
    }
}
