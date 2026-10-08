using Xunit.Sdk;

namespace Guinevere.Tests.Golden;

/// <summary>Tests for the <see cref="GoldenImage"/> harness itself.</summary>
public class GoldenImageTests
{
    static SKImage Solid(SKColor color, int size = 8)
    {
        var bitmap = new SKBitmap(size, size);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>A missing golden is written and reported; an equal image passes; a different one fails with a diff.</summary>
    [Fact]
    public void Harness_CreatesMatchesAndDiffs()
    {
        var dir = Path.Combine(GoldenImage.RepositoryRoot(), "Guinevere.Tests", "Golden", "SelfTest");
        var name = Path.Combine("SelfTest", $"probe-{Guid.NewGuid():N}");
        try
        {
            using var red = Solid(SKColors.Red);
            using var nearlyRed = Solid(new SKColor(254, 1, 1));
            using var blue = Solid(SKColors.Blue);
            using var larger = Solid(SKColors.Red, 9);

            Assert.Throws<FalseException>(() => GoldenImage.AssertMatches(name, red));
            GoldenImage.AssertMatches(name, nearlyRed);
            Assert.Throws<FailException>(() => GoldenImage.AssertMatches(name, blue));
            Assert.True(File.Exists(Path.Combine(GoldenImage.RepositoryRoot(), "Guinevere.Tests", "Golden", name + ".diff.png")));
            Assert.Throws<FailException>(() => GoldenImage.AssertMatches(name, larger));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
