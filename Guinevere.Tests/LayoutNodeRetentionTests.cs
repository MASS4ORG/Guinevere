using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks that immediate-mode nodes are reused across frames without carrying state between elements.</summary>
public class LayoutNodeRetentionTests
{
    /// <summary>A node rebuilt with the same identity is the same object, in both passes and across frames.</summary>
    [Fact]
    public void SameIdentity_ReusesTheNode()
    {
        using var harness = new FrameHarness();
        var seen = new List<(LayoutNode Parent, LayoutNode Child)>();
        void Draw(Gui gui)
        {
            using var parent = gui.Node(100, 50, "parent").Enter();
            seen.Add((gui.CurrentNode, gui.Node(10, 10)));
        }

        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(4, seen.Count);
        Assert.All(seen, pair => Assert.Same(seen[0].Parent, pair.Parent));
        Assert.All(seen, pair => Assert.Same(seen[0].Child, pair.Child));
    }

    /// <summary>A reused node starts from a new node's state: style, scope values, interaction and absolute flow.</summary>
    [Fact]
    public void ReusedNode_StartsFromANewNodesState()
    {
        using var harness = new FrameHarness();
        var decorate = true;
        LayoutNode? box = null, sibling = null;
        void Draw(Gui gui)
        {
            using (gui.Node(200, 100, "row").Direction(Axis.Horizontal).Enter())
            {
                box = gui.Node(40, 20, "box");
                if (decorate)
                {
                    box.Padding(5).Absolute(150, 50).Cursor(PointerCursor.Hand).HitTestVisible(false);
                    box.Scope.SetZIndex(7);
                    gui.SetTextColor(Color.Red, box.Scope);
                    box.On<ClickEvent>(_ => { });
                }
                sibling = gui.Node(40, 20, "sibling");
            }
        }

        harness.Frame(Draw);
        var first = box!;
        Assert.True(first.Style.IsAbsolute);
        decorate = false;
        harness.Frame(Draw);

        Assert.Same(first, box);
        Assert.False(box!.Style.IsAbsolute);
        Assert.Equal(0f, box.Style.PaddingLeft);
        Assert.Equal(0, box.Scope.Get<LayoutNodeScopeZIndex>().Value);
        Assert.Equal(sibling!.Scope.Get<LayoutNodeScopeTextColor>().Value,
            box.Scope.Get<LayoutNodeScopeTextColor>().Value);
        Assert.Null(box.CursorShape);
        Assert.True(box.IsHitTestVisible);
        Assert.False(box.HasListeners);
        Assert.Equal(new Rect(0, 0, 40, 20), box.Rect);
        Assert.Equal(40f, sibling.Rect.X);
    }

    /// <summary>A removed node is dropped: no other element receives its object or its state.</summary>
    [Fact]
    public void RemovedNode_IsNeverReusedForAnotherElement()
    {
        using var harness = new FrameHarness();
        var showFirst = true;
        var nodes = new List<LayoutNode>();
        void Draw(Gui gui)
        {
            if (gui.Pass != Pass.Pass1Build) return;
            nodes.Clear();
            if (showFirst)
            {
                var first = gui.Node(40, 20, "first").Absolute(100, 10);
                first.Scope.SetZIndex(9);
                nodes.Add(first);
            }
            else nodes.Add(gui.Node(40, 20, "replacement"));
            nodes.Add(gui.Node(40, 20, "kept"));
        }

        harness.Frame(Draw);
        var removed = nodes[0];
        var kept = nodes[1];
        showFirst = false;
        harness.Frame(Draw);

        Assert.NotSame(removed, nodes[0]);
        Assert.Same(kept, nodes[1]);
        Assert.False(nodes[0].Style.IsAbsolute);
        Assert.Equal(0, nodes[0].Scope.Get<LayoutNodeScopeZIndex>().Value);
        Assert.DoesNotContain(removed, harness.Gui.RootNode!.Children);
    }

    /// <summary>Inherited values follow the parent every frame; a value set once does not linger.</summary>
    [Fact]
    public void InheritedValues_FollowTheParentEachFrame()
    {
        using var harness = new FrameHarness();
        Color? parentColor = Color.Red;
        var childColor = Color.Black;
        var fresh = Color.Black;
        void Draw(Gui gui)
        {
            using (gui.Node(100, 100, "parent").Enter())
            {
                if (parentColor is { } color) gui.SetTextColor(color);
                childColor = gui.Node(10, 10, "child").Scope.Get<LayoutNodeScopeTextColor>().Value;
            }
            fresh = gui.Node(10, 10, $"probe{gui.Time.Frames}").Scope.Get<LayoutNodeScopeTextColor>().Value;
        }

        harness.Frame(Draw);
        Assert.Equal(Color.Red, childColor);
        parentColor = null;
        harness.Frame(Draw);
        Assert.Equal(fresh, childColor);
        parentColor = Color.Blue;
        harness.Frame(Draw);
        Assert.Equal(Color.Blue, childColor);
    }

    /// <summary>Listeners registered every frame on a reused node fire once per event.</summary>
    [Fact]
    public void Listeners_DoNotAccumulate()
    {
        using var harness = new FrameHarness();
        var clicks = 0;
        void Draw(Gui gui)
        {
            using var button = gui.Node(100, 100, "button").On<ClickEvent>(_ => clicks++).Enter();
        }

        for (var i = 0; i < 10; i++) harness.Frame(Draw);
        harness.Click(Draw, new Vector2(50, 50));
        harness.Frame(Draw);
        Assert.Equal(1, clicks);
    }

    /// <summary>Reordered keyed items keep their nodes and come back in the new order.</summary>
    [Fact]
    public void ReorderedItems_KeepTheirNodes()
    {
        using var harness = new FrameHarness();
        int[] order = [1, 2, 3];
        var byKey = new Dictionary<int, LayoutNode>();
        void Draw(Gui gui)
        {
            foreach (var key in order)
            {
                using var item = gui.ItemKey(key);
                byKey[key] = gui.Node(10, 10);
            }
        }

        harness.Frame(Draw);
        var before = new Dictionary<int, LayoutNode>(byKey);
        order = [3, 1, 2];
        harness.Frame(Draw);

        foreach (var key in order) Assert.Same(before[key], byKey[key]);
        Assert.Equal(order.Select(key => before[key]), harness.Gui.RootNode!.Children);
    }

    /// <summary>A node built only in the render pass is reused too.</summary>
    [Fact]
    public void RenderOnlyNode_IsReused()
    {
        using var harness = new FrameHarness();
        var seen = new List<LayoutNode>();
        void Draw(Gui gui)
        {
            if (gui.Pass == Pass.Pass2Render) seen.Add(gui.Node(10, 10, "overlay"));
        }

        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Same(seen[0], seen[1]);
    }

    /// <summary>Rebuilding an unchanged tree allocates almost nothing per node.</summary>
    [Fact]
    public void SteadyFrames_AllocateAlmostNothingPerNode()
    {
        const int count = 1000;
        const int frames = 20;
        using var harness = new FrameHarness();
        void Draw(Gui gui)
        {
            using (gui.Node(-1, -1, "list").Direction(Axis.Horizontal).Wrap(1).Enter())
                for (var i = 0; i < count; i++)
                    gui.Node(4, 4);
        }

        for (var i = 0; i < frames; i++) harness.Frame(Draw);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < frames; i++) harness.Frame(Draw);
        var perNode = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)(frames * count);

        Assert.True(perNode < 8, $"{perNode:F1} bytes per node per frame");
    }
}
