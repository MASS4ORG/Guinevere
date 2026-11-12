using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Guinevere;
using Guinevere.DevTools;
using SkiaSharp;

/// <summary>Exercises the actual native host gate while its application callback is paused.</summary>
sealed class NativeProfilerProbe : IGuiFrameController, IDisposable
{
    readonly Gui _gui;
    readonly ProfilerTarget _target;
    readonly Action _close;
    readonly Func<bool> _inputObserved;
    readonly Stopwatch _timeout = Stopwatch.StartNew();
    long _generation;
    float _elapsed;
    int _frames;

    internal NativeProfilerProbe(Gui gui, ProfilerTarget target, Action close, Func<bool> inputObserved)
    {
        _gui = gui;
        _target = target;
        _close = close;
        _inputObserved = inputObserved;
        gui.FrameController = this;
    }

    /// <inheritdoc />
    public void UpdateTime(double deltaSeconds) => _target.Controller.UpdateTime(deltaSeconds);
    /// <inheritdoc />
    public void Input(in GuiInputEvent input) => _target.Controller.Input(input);
    /// <inheritdoc />
    public void RenderFrame(SKCanvas canvas, Action draw)
    {
        _frames++;
        if (_frames == 1) { ReportHost(canvas); }
        if (_frames == 3)
        {
            _target.Controller.Paused = true;
            _generation = _target.Profiler.Latest!.Generation;
            _elapsed = _gui.Time.Elapsed;
        }
        if (_frames == 4)
        {
            if (_target.Profiler.Latest!.Generation != _generation || _gui.Time.Elapsed != _elapsed)
            {
                throw new InvalidOperationException("Native host advanced its paused target.");
            }
            _target.Controller.Step(.025);
        }
        _target.Controller.RenderFrame(canvas, draw);
        if (_frames == 4 && (_target.Profiler.Latest!.Generation != _generation + 1
            || Math.Abs(_gui.Time.Elapsed - _elapsed - .025f) > .00001f))
        {
            throw new InvalidOperationException("Native single-step advanced an incorrect frame/time boundary.");
        }
        if (_frames == 6) { _target.Controller.Paused = false; }
        if (_target.Controller.LastError is { } error) { throw new InvalidOperationException(error); }
        if (_frames >= 120 && _inputObserved())
        {
            if (_target.Controller.ScriptFailures != 0)
            {
                throw new InvalidOperationException(_target.Controller.PlaybackMessage);
            }
            Console.WriteLine("Native gate passed: frozen callbacks/time, one step, legacy replay, actual keyboard input.");
            _close();
        }
        if (_timeout.Elapsed.TotalSeconds > 15)
        {
            throw new TimeoutException("Native gate did not observe its scripted host keyboard input.");
        }
    }

    static void ReportHost(SKCanvas canvas)
    {
        var host = typeof(GuiWindow).Assembly;
        var configuration = host.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        using var image = canvas.Surface.Snapshot();
        Console.WriteLine($"{RuntimeInformation.OSDescription}; {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Host {host.Location}; configuration={configuration}; texture-backed={image.IsTextureBacked}");
        Console.WriteLine($"Host SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(host.Location)))}");
    }

    /// <inheritdoc />
    public void Dispose() => _gui.FrameController = _target.Controller;
}
