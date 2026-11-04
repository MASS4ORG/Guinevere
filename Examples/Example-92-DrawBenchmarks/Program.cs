using System.Diagnostics;
using Guinevere;

if (args.Contains("--samples"))
{
    BenchmarkSamples.Run(args);
    return;
}

var quick = args.Contains("--quick");
var count = quick ? 100 : 1_000;
var iterations = quick ? 30 : 100;
var backendIndex = Array.IndexOf(args, "--backend");
var backend = backendIndex >= 0
    ? backendIndex + 1 < args.Length ? args[backendIndex + 1] : throw new ArgumentException("--backend needs a value.")
    : "raster";
Console.WriteLine($"Runtime: {Environment.Version}; backend: {backend}; primitives: {count * 3}");
Console.WriteLine("| Scenario | Mean ms | Alloc B/op | Sort ms | Replay ms | " +
                  "Primitive draws | Drawables | Callbacks |");
Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");

using (var scene = new DrawScene(1100, 850, backend))
{
    void Primitives(Gui gui)
    {
        for (var i = 0; i < count; i++)
        {
            var x = i % 25 * 40f;
            var y = i / 25 * 20f;
            gui.DrawRect(new Rect(x, y, 30, 15), Color.Red, 3);
            gui.DrawCircle(new System.Numerics.Vector2(x + 15, y + 8), 4, Color.Blue);
            gui.DrawLine(new System.Numerics.Vector2(x, y), new System.Numerics.Vector2(x + 30, y + 15), Color.White);
        }
    }
    Action<Gui> primitives = Primitives;
    scene.Frame(primitives);
    scene.Gui.RootNode!.DrawList.EnsureCapacity(count * 3);
    Measure("primitive-record", iterations, () => scene.Record(primitives));
    Measure("primitive-replay", iterations, scene.Replay, scene.Gui, rootReplay: true);
    Measure("primitive-frame", iterations, () => scene.Frame(primitives), scene.Gui);
    scene.Dump(args, "primitives");
}

using (var scene = new DrawScene(1100, 850, backend))
{
    Measure("controls-frame", iterations, () => scene.Frame(DrawScenes.Controls), scene.Gui);
    scene.Dump(args, "controls");
    Measure("text-heavy-frame", iterations, () => scene.Frame(DrawScenes.TextHeavy), scene.Gui);
    scene.Dump(args, "text");
    Measure("clipped-frame", iterations, () => scene.Frame(DrawScenes.Clipped), scene.Gui);
    scene.Dump(args, "clipped");
    Measure("shadows-frame", iterations, () => scene.Frame(DrawScenes.Shadows), scene.Gui);
    scene.Dump(args, "shadows");
    Measure("shadows-record", iterations, () => scene.Record(DrawScenes.Shadows));
    Measure("shadows-replay", iterations, scene.Replay, scene.Gui, rootReplay: true);
    var dashboard = DrawScenes.Dashboard(scene.Gui);
    Measure("dashboard-frame", iterations, () => scene.Frame(dashboard), scene.Gui);
    scene.Dump(args, "dashboard");
}

using (var scene = new DrawScene(1100, 850, backend))
{
    var source = Shape.Rect(0, 0, 20, 10).SolidColor(Color.Blue);
    using var path = source.Path;
    using var paint = source.Paint;
    Action<Gui> clones = gui =>
    {
        for (var i = 0; i < 100; i++)
        {
            gui.DrawShape(new System.Numerics.Vector2(i % 25 * 30, i / 25 * 20), source);
        }
    };
    scene.Frame(clones);
    Measure("shape-clone-record", iterations, () => scene.Record(clones));
    Measure("shape-clone-replay", iterations, scene.Replay, scene.Gui, rootReplay: true);
}

using (var scene = new DrawScene(1100, 850, backend))
{
    foreach (var (name, draw) in new (string, Action<Gui>)[]
             { ("chart-lines", DrawScenes.ChartLines), ("chart-bars", DrawScenes.ChartBars) })
    {
        Measure(name, iterations, () => scene.Frame(draw), scene.Gui);
        scene.Dump(args, name);
    }
}

static void Measure(string name, int iterations, Action action, Gui? gui = null, bool rootReplay = false)
{
    for (var i = 0; i < 5; i++)
    {
        action();
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var watch = new Stopwatch();
    var before = GC.GetAllocatedBytesForCurrentThread();
    var sort = TimeSpan.Zero;
    var replay = TimeSpan.Zero;
    watch.Start();
    for (var i = 0; i < iterations; i++)
    {
        action();
        if (!rootReplay)
        {
            sort += gui?.RenderSortTime ?? TimeSpan.Zero;
            replay += gui?.RenderReplayTime ?? TimeSpan.Zero;
        }
    }
    watch.Stop();
    var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
    var stats = rootReplay ? gui!.RootNode!.DrawList.LastRenderStatistics : gui?.RenderStatistics ?? default;
    Console.WriteLine($"| {name} | {watch.Elapsed.TotalMilliseconds / iterations:F4} | {bytes} | " +
                      $"{sort.TotalMilliseconds / iterations:F4} | {replay.TotalMilliseconds / iterations:F4} | " +
                      $"{stats.PrimitiveDrawCalls} | {stats.Drawables} | {stats.Callbacks} |");
}
