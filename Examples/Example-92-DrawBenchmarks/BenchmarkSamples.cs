using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Guinevere;

/// <summary>Emits repeated frame samples and pixel hashes for comparisons between builds.</summary>
static class BenchmarkSamples
{
    /// <summary>Measures representative workloads with warmed resources and fixed frame counts.</summary>
    public static void Run(string[] args)
    {
        var index = Array.IndexOf(args, "--backend");
        var backend = index < 0 ? "raster" : args[index + 1];
        var quick = args.Contains("--quick");
        var iterations = quick ? 30 : 200;
        var samples = quick ? 3 : 9;
        var scenarioIndex = Array.IndexOf(args, "--scenario");
        var synchronize = !args.Contains("--present");
        Console.WriteLine($"# {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; " +
                          $"runtime {Environment.Version}; backend {backend}; synchronized {synchronize}; " +
                          $"frames/sample {iterations}");
        Console.WriteLine("scenario,sample,ms,bytes,sortMs,replayMs,draws,drawables,callbacks,pixelHash");
        using var scene = new DrawScene(1100, 850, backend, synchronize);
        foreach (var (name, draw) in new (string, Action<Gui>)[]
                 {
                     ("chart-lines", DrawScenes.ChartLines), ("chart-bars", DrawScenes.ChartBars),
                     ("controls", DrawScenes.Controls), ("text", DrawScenes.TextHeavy),
                     ("clipped", DrawScenes.Clipped), ("shadows", DrawScenes.Shadows),
                     ("dashboard", DrawScenes.Dashboard(scene.Gui))
                 })
        {
            if (scenarioIndex >= 0 && name != args[scenarioIndex + 1])
            {
                continue;
            }
            for (var warmup = 0; warmup < 100; warmup++)
            {
                scene.Frame(draw);
            }
            var hash = Convert.ToHexString(SHA256.HashData(scene.Pixels(draw)));
            for (var sample = 0; sample < samples; sample++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var sort = TimeSpan.Zero;
                var replay = TimeSpan.Zero;
                var bytes = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                for (var frame = 0; frame < iterations; frame++)
                {
                    scene.Frame(draw);
                    sort += scene.Gui.RenderSortTime;
                    replay += scene.Gui.RenderReplayTime;
                }
                var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds / iterations;
                bytes = (GC.GetAllocatedBytesForCurrentThread() - bytes) / iterations;
                var stats = scene.Gui.RenderStatistics;
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{name},{sample},{ms:F6},{bytes},{sort.TotalMilliseconds / iterations:F6}," +
                    $"{replay.TotalMilliseconds / iterations:F6},{stats.PrimitiveDrawCalls}," +
                    $"{stats.Drawables},{stats.Callbacks},{hash}"));
            }
        }
    }
}
