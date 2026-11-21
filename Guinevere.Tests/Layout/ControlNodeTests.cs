using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Layout;

/// <summary>The widget node: converts to <c>bool</c> as "an event this frame" and is reused only as a control.</summary>
public class ControlNodeTests
{
    [Fact]
    public void ConvertsToTrueOnlyWhileASignalIsRaised()
    {
        using var harness = new FrameHarness();
        bool idle = true, raised = false;
        harness.Frame(g =>
        {
            var node = g.StyledControl("widget");
            idle = node;
            node.Raise(NodeSignals.Changed);
            raised = node;
        });

        Assert.False(idle);
        Assert.True(raised);
        Assert.False((ControlNode?)null);
    }

    [Fact]
    public void ButtonReadsLikeABoolAndChainsAsANode()
    {
        using var harness = new FrameHarness(200, 100);
        bool direct = false, chained = false;

        harness.Click(g =>
        {
            if (g.Button("Plain"))
            {
                direct = true;
            }
        }, new Vector2(10, 10));
        harness.Click(g => chained |= g.Button("Go").Width(60).Height(30).Clicked, new Vector2(20, 20));

        Assert.True(direct);
        Assert.True(chained);
    }

    [Fact]
    public void ARetainedNodeOfTheOtherKindIsNotReused()
    {
        using var harness = new FrameHarness();
        var control = true;
        LayoutNode? first = null, second = null;

        harness.Frame(g => first = Build(g, control));
        control = false;
        harness.Frame(g => second = Build(g, control));

        Assert.IsType<ControlNode>(first);
        Assert.IsNotType<ControlNode>(second);
        Assert.NotSame(first, second);
        return;

        static LayoutNode Build(Gui g, bool control) =>
            control ? g.StyledControl("widget", id: "same") : g.StyledNode("widget", id: "same");
    }
}
