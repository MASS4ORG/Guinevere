using System.Diagnostics;
using Guinevere;

var quick = args.Contains("--quick");
var count = quick ? 100 : 1_000;
var iterations = quick ? 30 : 100;
Console.WriteLine($"Runtime: {Environment.Version}; backend: Skia raster; primitives per recording: {count * 3}");
Console.WriteLine("| Scenario | Mean ms | Alloc B/op |");
Console.WriteLine("|---|---:|---:|");

using (var scene = new DrawScene(1100, 850))
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
    Measure("primitive-replay", iterations, scene.Replay);
    Measure("primitive-frame", iterations, () => scene.Frame(primitives));
    scene.Dump(args, "primitives");
}

using (var scene = new DrawScene(1100, 850))
{
    Measure("controls-frame", iterations, () => scene.Frame(DrawScenes.Controls));
    scene.Dump(args, "controls");
    Measure("text-heavy-frame", iterations, () => scene.Frame(DrawScenes.TextHeavy));
    scene.Dump(args, "text");
    Measure("clipped-frame", iterations, () => scene.Frame(DrawScenes.Clipped));
    scene.Dump(args, "clipped");
    Measure("shadows-frame", iterations, () => scene.Frame(DrawScenes.Shadows));
    scene.Dump(args, "shadows");
    var dashboard = DrawScenes.Dashboard(scene.Gui);
    Measure("dashboard-frame", iterations, () => scene.Frame(dashboard));
    scene.Dump(args, "dashboard");
}

static void Measure(string name, int iterations, Action action)
{
    for (var i = 0; i < 5; i++) action();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var watch = new Stopwatch();
    var before = GC.GetAllocatedBytesForCurrentThread();
    watch.Start();
    for (var i = 0; i < iterations; i++) action();
    watch.Stop();
    var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
    Console.WriteLine($"| {name} | {watch.Elapsed.TotalMilliseconds / iterations:F4} | {bytes} |");
}
