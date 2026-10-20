using System.Diagnostics;
using Guinevere;
using SkiaSharp;

/// <summary>Measures ordinary-label layout reuse and invalidation through complete GUI passes.</summary>
static class TextLayoutBenchmarks
{
    enum Scenario { Repeated, Unique, Width, Font, Unicode }

    /// <summary>Runs label workloads with fixed iteration counts so allocation columns exclude timer polling.</summary>
    internal static void Run()
    {
        foreach (var scenario in Enum.GetValues<Scenario>())
        {
            Run(scenario);
        }
    }

    static void Run(Scenario scenario)
    {
        const int count = 1000;
        const int iterations = 100;
        using var surface = SKSurface.Create(new SKImageInfo(1000, 1000));
        var gui = new Gui { Input = new ScriptedInputHandler() };
        var first = Font.FromFamilyName("sans-serif");
        var second = Font.FromFamilyName("monospace");
        var labels = Enumerable.Range(0, count).Select(i => scenario switch
        {
            Scenario.Unique => $"Label {i}: a value with several words",
            Scenario.Unicode => "Latin e\u0301 — Ελληνικά — 漢字 — 🙂",
            _ => "A label with several words to wrap",
        }).ToArray();
        var options = new TextLayoutOptions { WrapMode = TextWrapMode.None };
        var frame = 0;
        var checksum = 0f;
        for (var i = 0; i < 1000; i++)
        {
            Frame();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var stopwatch = new Stopwatch();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Start();
        for (var i = 0; i < iterations; i++)
        {
            Frame();
        }

        stopwatch.Stop();
        var allocated = (GC.GetAllocatedBytesForCurrentThread() - allocatedBefore) / iterations;
        var meanMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
        Console.WriteLine($"| text-labels-{scenario.ToString().ToLowerInvariant()}-{count} | {count} | "
            + $"{meanMs:F4} | {meanMs * 1_000_000d / count:F2} | {allocated} |");
        GC.KeepAlive(checksum);
        first.Dispose();
        second.Dispose();

        void Frame()
        {
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.EndFrame();
            frame++;
        }

        void Draw()
        {
            gui.SetTextFont(scenario == Scenario.Font && frame % 2 != 0 ? second : first);
            var width = scenario == Scenario.Width ? (frame % 2 == 0 ? 100f : 160f) : 0f;
            for (var i = 0; i < labels.Length; i++)
            {
                if (width == 0f)
                {
                    checksum += gui.MeasureTextWidth(labels[i], 14);
                }

                gui.DrawText(labels[i], 14, wrapWidth: width, centerInRect: false,
                    layout: width > 0f ? null : options);
            }
        }
    }
}
