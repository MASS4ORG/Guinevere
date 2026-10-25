using System.Diagnostics;
using System.Reflection;
using Guinevere;
using SkiaSharp;

/// <summary>Fixed-frame font, changing-label and raster HUD workloads with native-instance diagnostics.</summary>
static class FontCacheBenchmarks
{
    static readonly Func<Font, SKFont> NativeFont = typeof(Font).GetProperty("SkFont",
        BindingFlags.Instance | BindingFlags.NonPublic)!.GetMethod!.CreateDelegate<Func<Font, SKFont>>();
    static readonly Func<Gui, int>? NativeBuilds = typeof(Gui).GetProperty("TextFontBuilds",
        BindingFlags.Instance | BindingFlags.NonPublic)?.GetMethod?.CreateDelegate<Func<Gui, int>>();

    /// <summary>Reports five timing samples and a separate font-identity probe, outside allocation measurements.</summary>
    internal static void Run()
    {
        foreach (var scenario in new[] { "access", "access-layout", "changing", "unicode", "hud", "static-hud" })
        {
            Run(scenario);
        }
    }

    static void Run(string scenario)
    {
        var access = scenario.StartsWith("access", StringComparison.Ordinal);
        var raster = scenario.EndsWith("hud", StringComparison.Ordinal);
        var count = access ? 1000 : raster ? 8 : 100;
        const int iterations = 100;
        using var surface = SKSurface.Create(new SKImageInfo(1000, 1000));
        var gui = new Gui { Input = new ScriptedInputHandler() };
        var source = Font.FromFamilyName("sans-serif");
        gui.ConfigureFonts(source);
        var fonts = new HashSet<SKFont>(ReferenceEqualityComparer.Instance);
        var layouts = new HashSet<TextLayout>(ReferenceEqualityComparer.Instance);
        var frame = 0;
        var checksum = 0f;
        for (var i = 0; i < 200; i++)
        {
            Frame(false);
        }

        var times = new double[5];
        var bytes = new long[5];
        var buildsBefore = NativeBuilds?.Invoke(gui);
        for (var sample = 0; sample < times.Length; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var watch = new Stopwatch();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            watch.Start();
            for (var i = 0; i < iterations; i++)
            {
                Frame(false);
            }

            watch.Stop();
            bytes[sample] = (GC.GetAllocatedBytesForCurrentThread() - allocated) / iterations;
            times[sample] = watch.Elapsed.TotalMilliseconds / iterations;
        }

        var builds = buildsBefore is { } prior ? (NativeBuilds!(gui) - prior).ToString() : "unavailable";
        for (var i = 0; i < 10; i++)
        {
            Frame(true);
        }

        Array.Sort(times);
        Array.Sort(bytes);
        Console.WriteLine($"font-{scenario}-{count}: median={times[2]:F4} ms/frame "
            + $"range={times[0]:F4}..{times[^1]:F4}; {bytes[2]} B/frame; "
            + $"10-frame probe={fonts.Count} returned native fonts, {layouts.Count} layouts; "
            + $"native builds={builds} / {iterations * times.Length} measured frames");
        GC.KeepAlive(checksum);
        source.Dispose();

        void Frame(bool probe)
        {
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw(probe);
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw(probe);
            if (raster)
            {
                surface.Canvas.Clear(SKColors.Transparent);
                gui.Render();
            }

            gui.EndFrame();
            frame++;
        }

        void Draw(bool probe)
        {
            for (var i = 0; i < count; i++)
            {
                if (access || raster)
                {
                    var font = gui.GetTextFont(14);
                    checksum += font.Size;
                    if (probe)
                    {
                        fonts.Add(NativeFont(font));
                    }
                }

                if (scenario == "access")
                {
                    continue;
                }

                var text = scenario switch
                {
                    "access-layout" or "static-hud" => "Health: 100 / 100",
                    "unicode" => $"{frame}:{i} Latin e\u0301 — Ελληνικά — 漢字 — 🙂",
                    _ => $"Health {i}: {frame}",
                };
                var layout = gui.LayoutText(text, size: 14);
                checksum += layout.Width;
                if (probe)
                {
                    layouts.Add(layout);
                    foreach (var run in layout.Runs)
                    {
                        fonts.Add(NativeFont(run.Font));
                    }
                }

                gui.DrawText(text, 14, centerInRect: false);
            }
        }
    }
}
