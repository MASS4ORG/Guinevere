using System.Text;
using SkiaSharp;

namespace Guinevere;

/// <summary>Expands legacy scripts into frame input while preserving post-frame checks and dump commands.</summary>
public sealed class InputScriptRecording
{
    readonly Dictionary<int, InputScriptStep> _checks;

    InputScriptRecording(InputRecording recording, Dictionary<int, InputScriptStep> checks)
    {
        Recording = recording;
        _checks = checks;
    }

    /// <summary>Frame boundaries corresponding to legacy playback's 60 Hz steps.</summary>
    public InputRecording Recording { get; }

    /// <summary>Compiles without executing the application or performing file operations.</summary>
    public static InputScriptRecording Compile(InputScript script, int maximumFrames = 60000,
        long maximumBytes = 64 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (script.Steps is null || script.Steps.Any(static step => step is null))
        {
            throw new InvalidDataException("Script steps are missing or malformed.");
        }
        var input = new ScriptedInputHandler();
        var frames = new List<RecordedInputFrame>();
        var checks = new Dictionary<int, InputScriptStep>();
        long bytes = 0;
        void Capture()
        {
            var frame = InputFrameRecorder.Capture(input, 1d / 60, script.Width, script.Height,
                game: input, touch: input);
            bytes += frame.RetainedBytes;
            if (frames.Count >= maximumFrames || bytes > maximumBytes)
            {
                throw new InvalidDataException("Script exceeds playback retention limits.");
            }
            frames.Add(frame);
            input.NewFrame();
        }
        foreach (var step in script.Steps)
        {
            if (step.Action == InputAction.Frames)
            {
                for (var count = 0; count < Math.Max(1, step.Count); count++) { Capture(); }
                continue;
            }
            ApplyStep(input, step, Capture, checks, frames.Count);
            Capture();
        }
        return new InputScriptRecording(new InputRecording(frames), checks);
    }

    static void ApplyStep(ScriptedInputHandler input, InputScriptStep step, Action capture,
        Dictionary<int, InputScriptStep> checks, int frame)
    {
        if (step.Action is InputAction.Click or InputAction.Key)
        {
            if (step.Action == InputAction.Click) { input.PressButton(step.Button); }
            else { input.PressKey(step.Key); }
            capture();
            if (step.Action == InputAction.Click) { input.ReleaseButton(step.Button); }
            else { input.ReleaseKey(step.Key); }
        }
        else if (!ApplyInput(input, step))
        {
            AddCheck(step, checks, frame);
        }
    }

    static void AddCheck(InputScriptStep step, Dictionary<int, InputScriptStep> checks, int frame)
    {
        if (step.Action is not (InputAction.Dump or InputAction.Tree or InputAction.ExpectCapture
            or InputAction.ExpectNode or InputAction.ExpectNoNode))
        {
            throw new InvalidDataException($"Unsupported script action: {step.Action}");
        }
        checks.Add(frame, step);
    }

    static bool ApplyInput(ScriptedInputHandler input, InputScriptStep step)
    {
        switch (step.Action)
        {
            case InputAction.Move: input.MoveTo(step.X, step.Y); break;
            case InputAction.Press: input.PressButton(step.Button); break;
            case InputAction.Release: input.ReleaseButton(step.Button); break;
            case InputAction.Scroll: input.Scroll(step.Delta); break;
            default: return ApplyKeyboard(input, step);
        }
        return true;
    }

    static bool ApplyKeyboard(ScriptedInputHandler input, InputScriptStep step)
    {
        switch (step.Action)
        {
            case InputAction.Hold: input.PressKey(step.Key); break;
            case InputAction.Unhold: input.ReleaseKey(step.Key); break;
            case InputAction.Type: input.TypeText(step.Text ?? ""); break;
            case InputAction.Reset: input.Reset(); break;
            default: return ApplyDevice(input, step);
        }
        return true;
    }

    static bool ApplyDevice(ScriptedInputHandler input, InputScriptStep step)
    {
        switch (step.Action)
        {
            case InputAction.ControllerConnect: input.ConnectController(step.DeviceId, step.Text ?? "Scripted controller"); break;
            case InputAction.ControllerDisconnect: input.DisconnectController(step.DeviceId); break;
            case InputAction.ControllerButton: input.SetControllerButton(step.DeviceId, step.GameButton, step.Down); break;
            case InputAction.ControllerAxis: input.SetControllerAxis(step.DeviceId, step.Axis, step.AxisValue); break;
            case InputAction.Touch:
                input.Touch(new TouchContact(step.ContactId,
                new Vector2(step.X, step.Y), step.Pressure, step.Phase)); break;
            case InputAction.Gesture:
                input.Gesture(new TouchGesture(step.GestureKind, step.ContactId,
                new Vector2(step.X, step.Y), Scale: step.Scale, Rotation: step.Rotation)); break;
            default: return false;
        }
        return true;
    }

    /// <summary>Runs the legacy checkpoint after its target frame, returning a copied diagnostic when present.</summary>
    public ScriptCheckpointResult? Check(int frameIndex, Gui gui, SKSurface surface, string? outputDirectory = null)
    {
        using var image = surface.Snapshot();
        return Check(frameIndex, gui, image, outputDirectory);
    }

    /// <summary>Runs a legacy checkpoint against the target's already captured image.</summary>
    public ScriptCheckpointResult? Check(int frameIndex, Gui gui, SKImage image, string? outputDirectory = null)
    {
        if (!_checks.TryGetValue(frameIndex, out var step))
        {
            return null;
        }
        return step.Action switch
        {
            InputAction.ExpectCapture => CheckCapture(step.Value, gui.PointerCapture),
            InputAction.ExpectNode or InputAction.ExpectNoNode => CheckNode(step, gui.RootNode),
            InputAction.Dump => Dump(step.Path, image, outputDirectory),
            InputAction.Tree => new ScriptCheckpointResult(true, DescribeTree(gui.RootNode)),
            _ => throw new InvalidOperationException("Unknown checkpoint.")
        };
    }

    static ScriptCheckpointResult CheckCapture(string? expected, string? actual)
    {
        var empty = string.IsNullOrEmpty(expected) || expected.Equals("none", StringComparison.OrdinalIgnoreCase);
        var success = empty ? actual is null : actual?.Contains(expected!, StringComparison.Ordinal) == true;
        return new ScriptCheckpointResult(success, $"expectCapture '{expected}': {actual ?? "none"}");
    }

    static ScriptCheckpointResult CheckNode(InputScriptStep step, LayoutNode? root)
    {
        var exists = FindNode(root, step.Value);
        var success = !string.IsNullOrEmpty(step.Value) && exists == (step.Action == InputAction.ExpectNode);
        return new ScriptCheckpointResult(success, $"{step.Action} '{step.Value}': {(exists ? "found" : "absent")}");
    }

    static bool FindNode(LayoutNode? root, string? fragment)
    {
        if (root is null || string.IsNullOrEmpty(fragment)) { return false; }
        var nodes = new Stack<LayoutNode>();
        nodes.Push(root);
        while (nodes.TryPop(out var node))
        {
            if (node.Id.Contains(fragment, StringComparison.Ordinal)) { return true; }
            foreach (var child in node.ChildNodes) { nodes.Push(child); }
        }
        return false;
    }

    static ScriptCheckpointResult Dump(string? path, SKImage image, string? directory)
    {
        if (string.IsNullOrEmpty(path)) { return new ScriptCheckpointResult(false, "Dump requires a path."); }
        var target = directory is null ? path : Path.Combine(directory, path);
        if (Path.GetDirectoryName(target) is { Length: > 0 } parent) { Directory.CreateDirectory(parent); }
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(target);
        data.SaveTo(output);
        return new ScriptCheckpointResult(true, $"Wrote frame to {target}");
    }

    static string DescribeTree(LayoutNode? root)
    {
        var output = new StringBuilder();
        var nodes = new Stack<LayoutNode>();
        if (root is not null) { nodes.Push(root); }
        while (nodes.TryPop(out var node) && output.Length < 16384)
        {
            output.AppendLine($"{node.Id}: {node.Rect}");
            for (var i = node.ChildNodes.Count - 1; i >= 0; i--) { nodes.Push(node.ChildNodes[i]); }
        }
        return output.ToString();
    }
}

/// <summary>A copied post-frame script result.</summary>
/// <param name="Success">Whether the checkpoint passed.</param>
/// <param name="Message">The observation or diagnostic.</param>
public sealed record ScriptCheckpointResult(bool Success, string Message);
