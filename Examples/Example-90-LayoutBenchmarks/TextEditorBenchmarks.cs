using System.Diagnostics;
using Guinevere;
using SkiaSharp;

/// <summary>Fixed-frame, rasterized editor workloads; document creation is outside the measured actions.</summary>
static class TextEditorBenchmarks
{
    /// <summary>Measures large line counts, long individual lines and selection at the end of a document.</summary>
    public static void Run()
    {
        Console.WriteLine("| Editor scenario | Mean ms | Max ms | Alloc B/frame |");
        Console.WriteLine("|---|---:|---:|---:|");
        Measure("lines-100000", string.Join('\n', Enumerable.Repeat("int value = 123; // source", 100_000)), true);
        Measure("line-10000", new string('x', 10_000), false);
        Measure("line-100000", new string('x', 100_000), false);
        Measure("line-1000000", new string('x', 1_000_000), false);
        Measure("minified-csharp-20000", string.Concat(Enumerable.Repeat("var x = 123; ", 1600)), true);
        Measure("unicode-20000", string.Concat(Enumerable.Repeat("e\u0301", 10_000)), false);
    }

    static void Measure(string name, string text, bool syntax)
    {
        using var surface = SKSurface.Create(new SKImageInfo(1000, 600));
        var input = new ScriptedInputHandler();
        var gui = new BenchmarkGui(1000, 600) { Input = input };
        var language = syntax ? LanguageServices.ForLanguage("csharp") : LanguageServices.PlainText;
        var state = new TextEditorState(text, language with { Folding = null, Completion = null });
        var options = new TextEditorOptions { FontSize = 14 };
        Sample(name + "-first", 1, Frame);
        Sample(name + "-idle", 20, Frame);
        state.Execute("cursorBottom");
        Sample(name + "-end", 1, Frame);
        Sample(name + "-type", 20, () => { state.Type("x"); Frame(); });
        Sample(name + "-left", 20, () => { state.Execute("cursorLeft"); Frame(); });
        state.Execute("selectAll");
        Sample(name + "-select-all", 20, Frame);
        state.ScrollY = text.Length > 1_000_000 && syntax ? 1_000_000 : 0;
        Sample(name + "-scroll", 20, () => { state.ScrollY += 17; state.ScrollX += 53; Frame(); });

        void Frame()
        {
            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.Render();
            gui.EndFrame();
            input.NewFrame();
        }

        void Draw() => gui.TextEditor(state, options, 1000, 600);
    }

    static void Sample(string name, int frames, Action action)
    {
        var start = GC.GetAllocatedBytesForCurrentThread();
        var total = 0d;
        var max = 0d;
        for (var i = 0; i < frames; i++)
        {
            var stamp = Stopwatch.GetTimestamp();
            action();
            var elapsed = Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
            total += elapsed;
            max = Math.Max(max, elapsed);
        }

        Console.WriteLine($"| {name} | {total / frames:F3} | {max:F3} | "
            + $"{(GC.GetAllocatedBytesForCurrentThread() - start) / frames} |");
    }
}
