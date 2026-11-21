using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Layout;

/// <summary>Node signals: raised by a widget, read by its caller, cleared when the node is built again.</summary>
public class LayoutNodeInteractionTests
{
    [Fact]
    public void RaisedSignalsAreReadUntilTheNodeIsBuiltAgain()
    {
        using var harness = new FrameHarness();
        LayoutNode? built = null;
        var seenInRender = NodeSignals.None;
        var seenAfterRebuild = NodeSignals.Clicked;

        harness.Frame(g =>
        {
            var node = g.Node(10, 10);
            if (g.Pass == Pass.Pass1Build)
            {
                seenAfterRebuild = node.Signals;
                node.Raise(NodeSignals.Clicked);
                return;
            }

            seenAfterRebuild |= node.Signals;
            built = node.Raise(NodeSignals.Changed).Raise(NodeSignals.Submitted);
            seenInRender = built.Signals;
        });

        Assert.Equal(NodeSignals.None, seenAfterRebuild);
        Assert.Equal(NodeSignals.Changed | NodeSignals.Submitted, seenInRender);
        Assert.False(built!.Clicked);
        Assert.True(built.Changed);
        Assert.True(built.Submitted);
    }
}
