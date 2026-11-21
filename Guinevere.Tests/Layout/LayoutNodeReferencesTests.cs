using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Layout;

/// <summary>Node-relative sizes from <see cref="Size.PercentOf"/>: settling in one frame and diagnostics.</summary>
public class LayoutNodeReferencesTests
{
    static LayoutNode Find(Gui gui, string id) =>
        gui.RootNode!.FindChildById(id) ?? throw new InvalidOperationException($"node '{id}' not found");

    [Fact]
    public void SizeFollowsAnEarlierSibling()
    {
        using var frame = new FrameHarness();
        frame.Frame(static g =>
        {
            var source = g.Node(120, 40, "source");
            g.Node(-1, 10, "follower").Width(Size.PercentOf(source, 0.5f));
        });

        Assert.Equal(60f, Find(frame.Gui, "follower").Rect.W, 2);
    }

    [Fact]
    public void SizeFollowsALaterNodeWithinTheSameFrame()
    {
        using var frame = new FrameHarness();
        LayoutNode? follower = null;
        frame.Frame(g =>
        {
            follower = g.Node(-1, 10, "follower");
            var source = g.Node(200, 40, "source");
            follower.Width(Size.PercentOf(source, 0.5f));
        });

        Assert.Equal(100f, Find(frame.Gui, "follower").Rect.W, 2);
    }

    [Theory]
    [InlineData(0f, 100f)]
    [InlineData(0.25f, 125f)]
    [InlineData(1f, 200f)]
    public void ContainerBlendsTheHeightsOfDescendantTabs(float amount, float expected)
    {
        using var frame = new FrameHarness(400, 400);
        frame.Frame(g =>
        {
            using (g.Node(300, -1, "tabs").Enter())
            {
                using (g.Node().Direction(Axis.Horizontal).Enter())
                {
                    var first = g.Node(100, 100, "first");
                    var second = g.Node(100, 200, "second");
                    g.CurrentNode.Parent!.Height(Size.Lerp(Size.PercentOf(first), Size.PercentOf(second), amount));
                }
            }
        });

        Assert.Equal(expected, Find(frame.Gui, "tabs").Rect.H, 2);
    }

    [Fact]
    public void AddHeightAccumulatesWeightedReferences()
    {
        using var frame = new FrameHarness(400, 400);
        frame.Frame(static g =>
        {
            using (g.Node(300, -1, "tabs").Enter())
            {
                var first = g.Node(100, 100, "first");
                var second = g.Node(100, 200, "second");
                g.CurrentNode.AddHeight(Size.PercentOf(first), 0.5f).AddHeight(Size.PercentOf(second), 0.5f);
            }
        });

        Assert.Equal(150f, Find(frame.Gui, "tabs").Rect.H, 2);
    }

    [Fact]
    public void ConstraintsAcceptReferences()
    {
        using var frame = new FrameHarness();
        frame.Frame(static g =>
        {
            var source = g.Node(150, 20, "source");
            g.Node(10, 10, "bounded").MinWidth(Size.PercentOf(source) + 5f);
        });

        Assert.Equal(155f, Find(frame.Gui, "bounded").Rect.W, 2);
    }

    [Fact]
    public void ReferenceToANodeFromAnEarlierFrameIsReported()
    {
        using var frame = new FrameHarness();
        LayoutNode? old = null;
        frame.Frame(g => old = g.Node(100, 20, "old"));

        var error = Assert.Throws<InvalidOperationException>(() =>
            frame.Frame(g => g.Node(-1, 10, "reader").Width(Size.PercentOf(old!))));

        Assert.Contains("not part of the current frame", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceToItselfOnTheSameAxisIsReported()
    {
        using var frame = new FrameHarness();
        var error = Assert.Throws<InvalidOperationException>(() => frame.Frame(static g =>
        {
            var node = g.Node(-1, 10, "self");
            node.Width(Size.PercentOf(node, 0.5f));
        }));

        Assert.Contains("from itself", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceCycleWithoutAFixedPointIsReported()
    {
        using var frame = new FrameHarness();
        var error = Assert.Throws<InvalidOperationException>(() => frame.Frame(static g =>
        {
            var a = g.Node(-1, 10, "a");
            var b = g.Node(-1, 10, "b");
            a.Width(Size.PercentOf(b) + 10f);
            b.Width(Size.PercentOf(a) + 10f);
        }));

        Assert.Contains("did not settle", error.Message, StringComparison.Ordinal);
        Assert.Contains("'a'", error.Message, StringComparison.Ordinal);
    }
}
