using Guinevere.Tests.Mocks;
using GuinevereDemos;

namespace Guinevere.Tests.Examples;

/// <summary>Checks that the shared demo header loads and renders its embedded badge.</summary>
public class DemoHeaderTests
{
    /// <summary>Building and rendering successive headers succeeds without a badge file beside the executable.</summary>
    [Fact]
    public void Header_RendersWithEmbeddedBadge()
    {
        using var resource = typeof(DemoHeader).Assembly.GetManifestResourceStream("GuinevereDemos.guinevere-badge.png");
        Assert.NotNull(resource);
        using var frame = new FrameHarness();
        frame.Frame(gui => DemoHeader.Header(gui, "Demo"));
        frame.Frame(gui => DemoHeader.Header(gui, "Demo"));
    }
}
