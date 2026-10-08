namespace Guinevere.Tests.Golden;

/// <summary>
/// Compares rendered images with committed PNGs under <c>Guinevere.Tests/Golden</c>. A missing golden is written and
/// the comparison fails so it gets reviewed; set <c>GUINEVERE_UPDATE_GOLDENS=1</c> to rewrite goldens on purpose.
/// Mismatches leave <c>.actual.png</c> and <c>.diff.png</c> files beside the golden for inspection.
/// </summary>
public static class GoldenImage
{
    /// <summary>The largest per-channel difference still treated as equal (antialiasing noise).</summary>
    const int Tolerance = 2;

    static readonly string Root = Path.Combine(RepositoryRoot(), "Guinevere.Tests", "Golden");

    /// <summary>The repository root, found by walking up from the test binaries to <c>Guinevere.slnx</c>.</summary>
    public static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Guinevere.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>Asserts that <paramref name="image"/> matches the golden <paramref name="name"/> (a relative path).</summary>
    public static void AssertMatches(string name, SKImage image)
    {
        var golden = Path.Combine(Root, name + ".png");
        if (Environment.GetEnvironmentVariable("GUINEVERE_UPDATE_GOLDENS") == "1" || !File.Exists(golden))
        {
            var created = !File.Exists(golden);
            Write(golden, image);
            Assert.False(created, $"Golden '{name}' did not exist and was written; review it and re-run.");
            return;
        }

        using var expected = SKImage.FromEncodedData(golden);
        var mismatches = Compare(expected, image, out var diff);
        if (mismatches == 0) return;

        Write(Path.ChangeExtension(golden, ".actual.png"), image);
        if (diff is not null) Write(Path.ChangeExtension(golden, ".diff.png"), diff);
        Assert.Fail($"Golden '{name}' differs in {mismatches} pixels (tolerance {Tolerance}); see .actual/.diff files.");
    }

    /// <summary>Counts pixels whose channels differ by more than the tolerance and paints them red in a diff image.</summary>
    static int Compare(SKImage expected, SKImage actual, out SKImage? diff)
    {
        diff = null;
        if (expected.Width != actual.Width || expected.Height != actual.Height) return expected.Width * expected.Height;

        using var a = SKBitmap.FromImage(expected);
        using var b = SKBitmap.FromImage(actual);
        var marks = new SKBitmap(a.Width, a.Height);
        var count = 0;
        for (var y = 0; y < a.Height; y++)
            for (var x = 0; x < a.Width; x++)
            {
                var p = a.GetPixel(x, y);
                var q = b.GetPixel(x, y);
                var differs = Math.Abs(p.Red - q.Red) > Tolerance || Math.Abs(p.Green - q.Green) > Tolerance
                              || Math.Abs(p.Blue - q.Blue) > Tolerance || Math.Abs(p.Alpha - q.Alpha) > Tolerance;
                marks.SetPixel(x, y, differs ? SKColors.Red : p.WithAlpha(60));
                if (differs) count++;
            }

        diff = SKImage.FromBitmap(marks);
        return count;
    }

    static void Write(string path, SKImage image)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
