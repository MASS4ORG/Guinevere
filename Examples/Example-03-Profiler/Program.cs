using Guinevere;
using Guinevere.DevTools;
using Guinevere.Profiling;
using SkiaSharp;

var headless = args.Contains("--headless");
var smoke = args.Contains("--smoke");
var nativeGate = args.Contains("--gate-smoke");
using var session = new ProfileSession();
using var view = new ProfilerView(session);
var font = Font.FromFamilyName("sans-serif", 14);
var target = new Gui { Input = new ScriptedInputHandler() };
target.ConfigureFonts(font);
view.Gui.ConfigureFonts(font);
using var targetSurface = SKSurface.Create(new SKImageInfo(640, 360));
using var registration = view.Attach(target, "Counter application");
registration.Profiler.Detailed = true;
using var worker = session.Register("Engine-shaped producer", "engine");
var counter = 0;
var nativeInputObserved = false;
var hostFrames = 0;
long pausedGeneration = 0;
GuiWindow? window = null;
registration.Controller.ResetTarget = () => counter = 0;
registration.Controller.Recording = true;
registration.Controller.Play(new InputScript
{
    Width = 640,
    Height = 360,
    Steps = [new() { Action = InputAction.Frames, Count = 2 },
        new() { Action = InputAction.Key, Key = KeyboardKey.Tab },
        new() { Action = InputAction.Key, Key = KeyboardKey.Enter },
        new() { Action = InputAction.ExpectNode, Value = "increment" }]
});

void DrawTarget()
{
    using var scope = target.StyledNode("counter-app").Expand().Direction(Axis.Vertical).Padding(24).Enter();
    if (target.Input.IsKeyPressed(KeyboardKey.A)) { nativeInputObserved = true; }
    target.Label($"Counter: {counter}");
    if (target.Button("Increment", width: 180, height: 40, id: "increment")) { counter++; }
    target.SetData(counter, "counter");
    using (target.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Gap(4).Enter())
    {
        for (var row = 0; row < 120; row++)
        {
            target.Label($"Track {row} · {(counter + row) % 7}");
        }
    }
}

// Busy-waits to stand in for engine work, so the engine source shows nested scopes and periodic spikes.
static void Work(double milliseconds)
{
    var end = System.Diagnostics.Stopwatch.GetTimestamp()
        + (long)(milliseconds * System.Diagnostics.Stopwatch.Frequency / 1000);
    while (System.Diagnostics.Stopwatch.GetTimestamp() < end) { }
}

void EngineFrame()
{
    worker.BeginFrame(hostFrames);
    var spike = hostFrames % 45 == 0 ? 6 : 0;
    using (var update = worker.BeginScope("Update", track: 1))
    {
        using (worker.BeginScope("Physics", track: 1, parentId: update.Id)) { Work(.3 + spike); }
        using (worker.BeginScope("AI", track: 1, parentId: update.Id)) { Work(.2 + hostFrames % 7 * .03); }
        worker.Counter("Entities", 42, "count");
    }
    using (var render = worker.BeginScope("Render", track: 2))
    {
        using (worker.BeginScope("Cull", track: 2, parentId: render.Id)) { Work(.15); }
        using (worker.BeginScope("Submit", track: 2, parentId: render.Id)) { Work(.25); }
    }
    worker.EndFrame();
}

void DrawHost()
{
    if (view.Gui.Pass == Pass.Pass1Build)
    {
        hostFrames++;
        target.UpdateFrameTime(1d / 60);
        targetSurface.Canvas.Clear(SKColors.Transparent);
        target.RenderFrame(targetSurface.Canvas, DrawTarget);
        EngineFrame();
        if (smoke || headless) { Exercise(); }
    }
    view.Draw();
}

void Exercise()
{
    if (hostFrames == 10)
    {
        registration.Controller.Paused = true;
        pausedGeneration = registration.Profiler.Latest!.Generation;
    }
    if (hostFrames == 11)
    {
        if (registration.Profiler.Latest!.Generation != pausedGeneration)
        {
            throw new InvalidOperationException("Pause executed target callbacks.");
        }
        registration.Controller.Step(.025);
    }
    if (hostFrames == 12 && registration.Profiler.Latest!.Generation != pausedGeneration + 1)
    {
        throw new InvalidOperationException("Step did not execute exactly one target frame.");
    }
    if (hostFrames == 13) { view.SetSkin(ProfilerSkins.Light); }
    if (hostFrames == 14) { view.Command("panel", "input"); }
    if (hostFrames == 15) { view.Command("float"); }
    if (hostFrames == 16) { view.Command("dock-right"); }
    if (hostFrames == 18)
    {
        using var capture = new MemoryStream();
        view.Export(capture);
        capture.Position = 0;
        view.Import(capture);
    }
    if (hostFrames == 20)
    {
        if (registration.Controller.ScriptFailures != 0 || registration.Controller.LastError is not null)
        {
            throw new InvalidOperationException(registration.Controller.LastError ?? registration.Controller.PlaybackMessage);
        }
        Console.WriteLine($"Profiler smoke passed: pause/step, script, capture roundtrip, skins, docking; counter={counter}");
        window?.Close();
        registration.Controller.Paused = false;
        view.Command("live");
        view.Command("panel", "timeline");
    }
}

if (headless)
{
    view.Gui.Input = new ScriptedInputHandler();
    using var surface = SKSurface.Create(new SKImageInfo(1280, 840));
    for (var frame = 0; frame < 140; frame++)
    {
        view.Gui.UpdateFrameTime(1d / 60);
        view.Gui.RenderFrame(surface.Canvas, DrawHost);
    }
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    var output = args.FirstOrDefault(static arg => arg.EndsWith(".png", StringComparison.Ordinal)) ?? "profiler.png";
    using var file = File.Create(output);
    data.SaveTo(file);
}
else
{
    using var native = new GuiWindow(nativeGate ? target : view.Gui, 1280, 840, $"Guinevere profiler {Environment.ProcessId}");
    window = native;
    native.Pacing.OnDemand = false;
    if (nativeGate)
    {
        using var probe = new NativeProfilerProbe(target, registration, native.Close, () => nativeInputObserved);
        native.RunGui(DrawTarget);
    }
    else
    {
        native.RunGui(DrawHost);
    }
}
font.Dispose();
