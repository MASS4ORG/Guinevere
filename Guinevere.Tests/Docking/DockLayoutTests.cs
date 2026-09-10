namespace Guinevere.Tests.Docking;

/// <summary>
/// Model-level coverage for <see cref="DockLayout"/>: the tree edits a user's drags produce, and the
/// JSON a host persists between runs.
/// </summary>
public class DockLayoutTests
{
    private static DockLayout Seeded()
    {
        var layout = new DockLayout();
        layout.DockAtEdge("scene", DockZone.Center);
        layout.DockAtEdge("tree", DockZone.Left, 0.2f);
        layout.DockAtEdge("inspector", DockZone.Right, 0.25f);
        return layout;
    }

    [Fact]
    public void SeedingAnEdgeTwiceTabsIntoTheSameGroupInsteadOfNesting()
    {
        var layout = Seeded();
        layout.DockAtEdge("assets", DockZone.Left, 0.2f);

        var left = layout.FindLeaf("tree");
        Assert.NotNull(left);
        Assert.Equal(["tree", "assets"], left.PanelIds);
        Assert.Equal(3, layout.Leaves().Count());
    }

    [Theory]
    [InlineData(DockZone.Left, Axis.Horizontal, true)]
    [InlineData(DockZone.Right, Axis.Horizontal, false)]
    [InlineData(DockZone.Top, Axis.Vertical, true)]
    [InlineData(DockZone.Bottom, Axis.Vertical, false)]
    public void DockingOnAnEdgeSplitsTheTargetOnTheRightSide(DockZone zone, Axis axis, bool incomingFirst)
    {
        var layout = new DockLayout { Root = new DockLeaf("scene") };
        var target = layout.FindLeaf("scene")!;

        layout.DockInto("output", target, zone);

        var split = Assert.IsType<DockSplit>(layout.Root);
        Assert.Equal(axis, split.Axis);

        var incoming = incomingFirst ? split.First : split.Second;
        var stayed = incomingFirst ? split.Second : split.First;
        Assert.Equal(["output"], Assert.IsType<DockLeaf>(incoming).PanelIds);
        Assert.Same(target, stayed);
    }

    [Fact]
    public void DockingOnTheCentreJoinsTheTargetsTabsAndActivates()
    {
        var layout = Seeded();
        var centre = layout.FindLeaf("scene")!;

        layout.DockInto("inspector", centre, DockZone.Center);

        Assert.Equal(["scene", "inspector"], centre.PanelIds);
        Assert.Equal("inspector", centre.ActivePanelId);
        Assert.Equal(2, layout.Leaves().Count());
    }

    [Fact]
    public void DockingAPanelOntoItsOwnSoleGroupChangesNothing()
    {
        var layout = new DockLayout { Root = new DockLeaf("scene") };
        var leaf = layout.FindLeaf("scene")!;

        layout.DockInto("scene", leaf, DockZone.Left);

        Assert.Same(leaf, layout.Root);
        Assert.Equal(["scene"], leaf.PanelIds);
    }

    [Fact]
    public void RemovingTheLastPanelOfAGroupCollapsesItsSplit()
    {
        var layout = Seeded();

        Assert.True(layout.Remove("tree"));

        Assert.Null(layout.FindLeaf("tree"));
        Assert.Equal(["scene", "inspector"], layout.PanelIds);
        // scene | inspector remains as a single split, with the left column gone entirely.
        var split = Assert.IsType<DockSplit>(layout.Root);
        Assert.IsType<DockLeaf>(split.First);
        Assert.IsType<DockLeaf>(split.Second);
    }

    [Fact]
    public void RemovingEveryPanelLeavesAnEmptyLayout()
    {
        var layout = Seeded();

        foreach (var panelId in layout.PanelIds.ToList()) layout.Remove(panelId);

        Assert.Null(layout.Root);
        Assert.Empty(layout.PanelIds);
        Assert.False(layout.Remove("scene"));
    }

    [Fact]
    public void RemovingTheActiveTabKeepsTheGroupShowingSomething()
    {
        var leaf = new DockLeaf("a", "b", "c") { ActiveIndex = 2 };
        var layout = new DockLayout { Root = leaf };

        layout.Remove("c");

        Assert.Equal(["a", "b"], leaf.PanelIds);
        Assert.Equal("b", leaf.ActivePanelId);
    }

    [Fact]
    public void ReorderMovesATabAndKeepsTheSamePanelActive()
    {
        var leaf = new DockLeaf("a", "b", "c") { ActiveIndex = 0 };

        DockLayout.Reorder(leaf, 0, 2);

        Assert.Equal(["b", "c", "a"], leaf.PanelIds);
        Assert.Equal("a", leaf.ActivePanelId);
    }

    [Fact]
    public void FloatingTearsThePanelOutOfTheDockedTree()
    {
        var layout = Seeded();

        var window = layout.Float("inspector", new Rect(10, 20, 300, 200));

        Assert.Single(layout.Floating);
        Assert.Equal(["inspector"], Assert.IsType<DockLeaf>(window.Root).PanelIds);
        Assert.DoesNotContain("inspector", layout.Root!.Leaves().SelectMany(l => l.PanelIds));
        // Still part of the layout: it is findable and counted among the panels.
        Assert.True(layout.Contains("inspector"));
    }

    [Fact]
    public void ClosingTheLastPanelOfAFloatingWindowRemovesTheWindow()
    {
        var layout = Seeded();
        layout.Float("inspector", new Rect(10, 20, 300, 200));

        layout.Remove("inspector");

        Assert.Empty(layout.Floating);
    }

    [Fact]
    public void EnsurePanelAddsOnlyWhatIsMissing()
    {
        var layout = Seeded();

        layout.EnsurePanel("tree", DockZone.Left);
        layout.EnsurePanel("output", DockZone.Bottom, 0.3f);

        Assert.Single(layout.PanelIds, id => id == "tree");
        Assert.True(layout.Contains("output"));
    }

    [Fact]
    public void RemoveUnknownDropsPanelsTheHostNoLongerRegisters()
    {
        var layout = Seeded();

        layout.RemoveUnknown(["scene", "tree"]);

        Assert.Equal(["scene", "tree"], layout.PanelIds.Order());
        Assert.False(layout.Contains("inspector"));
    }

    [Fact]
    public void JsonRoundTripsTheTreeTheFractionsAndTheFloatingWindows()
    {
        var layout = Seeded();
        layout.Float("output", new Rect(12, 34, 300, 200));
        ((DockSplit)layout.Root!).Fraction = 0.37f;
        layout.FindLeaf("scene")!.ActiveIndex = 0;

        var restored = DockLayout.FromJson(layout.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(layout.PanelIds, restored.PanelIds);
        Assert.Equal(0.37f, ((DockSplit)restored.Root!).Fraction, 3);
        Assert.Single(restored.Floating);
        Assert.Equal(new Rect(12, 34, 300, 200), restored.Floating[0].Bounds);
    }

    [Fact]
    public void JsonFromAnotherVersionIsRefusedRatherThanGuessed()
    {
        var json = Seeded().ToJson().Replace($"\"version\": {DockLayout.CurrentVersion}", "\"version\": 99");

        Assert.Null(DockLayout.FromJson(json));
    }

    [Fact]
    public void MalformedJsonIsRefused()
    {
        Assert.Null(DockLayout.FromJson("{ not json"));
    }

    /// <summary>
    /// Walks the whole layout and fails if any node is reached twice. A cycle used to be created by
    /// docking across groups, and it surfaced as a stack overflow inside <c>DockNode.Descend()</c>
    /// rather than as anything pointing at the edit that caused it.
    /// </summary>
    private static void AssertAcyclic(DockLayout layout)
    {
        var seen = new HashSet<DockNode>(ReferenceEqualityComparer.Instance);

        void Visit(DockNode node)
        {
            Assert.True(seen.Add(node), $"{node.GetType().Name} is reachable twice — the tree has a cycle.");
            if (node is not DockSplit split) return;

            Visit(split.First);
            Visit(split.Second);
        }

        if (layout.Root is not null) Visit(layout.Root);
        foreach (var window in layout.Floating) Visit(window.Root);
    }

    [Theory]
    [InlineData(DockZone.Left)]
    [InlineData(DockZone.Right)]
    [InlineData(DockZone.Top)]
    [InlineData(DockZone.Bottom)]
    [InlineData(DockZone.Center)]
    public void DockingAPanelFromOneGroupOntoAnotherLeavesNoCycle(DockZone zone)
    {
        // Guards the reported crash: cross-group docks once spliced a split in as its own child.
        var layout = Seeded();
        var target = layout.FindLeaf("inspector")!;

        layout.DockInto("tree", target, zone);

        AssertAcyclic(layout);
        Assert.Equal(["inspector", "scene", "tree"], layout.PanelIds.Order());
        Assert.NotNull(layout.FindLeaf("tree"));
    }

    [Fact]
    public void DockingBetweenGroupsRepeatedlyStaysConsistent()
    {
        var layout = Seeded();

        // Shuffle a panel around the layout the way a user would, then check nothing is corrupt.
        layout.DockInto("tree", layout.FindLeaf("inspector")!, DockZone.Bottom);
        layout.DockInto("inspector", layout.FindLeaf("scene")!, DockZone.Center);
        layout.DockInto("scene", layout.FindLeaf("tree")!, DockZone.Right);

        AssertAcyclic(layout);
        Assert.Equal(["inspector", "scene", "tree"], layout.PanelIds.Order());
    }

    [Fact]
    public void ASplitRefusesToBecomeItsOwnChild()
    {
        var split = new DockSplit(Axis.Horizontal, new DockLeaf("a"), new DockLeaf("b"));

        Assert.Throws<ArgumentException>(() => split.First = split);
        Assert.Throws<ArgumentException>(() => split.Second = split);
    }
}
