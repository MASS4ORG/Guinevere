using Example02;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Examples;

/// <summary>Checks that the shared demo header loads and renders its embedded badge.</summary>
public class DemoHeaderTests
{
    /// <summary>Building and rendering successive headers succeeds without a badge file beside the executable.</summary>
    [Fact]
    public void HeaderRendersWithEmbeddedBadge()
    {
        using var resource = typeof(GraphPlayground).Assembly.GetManifestResourceStream("GuinevereDemos.guinevere-badge.png");
        Assert.NotNull(resource);
        using var frame = new FrameHarness();
        var playground = new GraphPlayground();
        frame.Frame(playground.Draw);
        frame.Frame(playground.Draw);
    }
}
