using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Checks that the render pass revisits the build pass's nodes, in order and when the passes diverge.</summary>
public class GuiLayoutNodeTests
{
    /// <summary>Runs one frame, recording each pass's nodes in creation order.</summary>
    static (List<LayoutNode> Build, List<LayoutNode> Render, LayoutNode Root) Frame(FrameHarness harness,
        Action<Gui, List<LayoutNode>> draw)
    {
        var build = new List<LayoutNode>();
        var render = new List<LayoutNode>();
        harness.Frame(gui => draw(gui, gui.Pass == Pass.Pass1Build ? build : render));
        return (build, render, harness.Gui.RootNode!);
    }

    /// <summary>Repeated call sites and explicit IDs map to the build nodes in the order they were created.</summary>
    [Fact]
    public void RenderPass_RevisitsBuildNodesInOrder()
    {
        using var harness = new FrameHarness();
        var (build, render, root) = Frame(harness, (gui, nodes) =>
        {
            for (var i = 0; i < 5; i++) nodes.Add(gui.Node(4, 4));
            nodes.Add(gui.Node(UnitValue.Pixels(4), UnitValue.Pixels(4), id: "b"));
            nodes.Add(gui.Node(4, 4, id: "a"));
        });

        Assert.Equal(build, render);
        Assert.Equal(build, root.ChildNodes);
        Assert.Equal(["b", "a"], root.ChildNodes.Skip(5).Select(n => n.Id));
    }

    /// <summary>
    /// Nodes only built in the render pass are appended, nodes the render pass skips stay, and swapped explicit IDs
    /// still find their own build nodes.
    /// </summary>
    [Fact]
    public void RenderPass_ResynchronizesWhenPassesDiverge()
    {
        using var harness = new FrameHarness();
        var (build, render, root) = Frame(harness, (gui, nodes) =>
        {
            var renderPass = gui.Pass == Pass.Pass2Render;
            nodes.Add(gui.Node(4, 4, id: "first"));
            if (renderPass) nodes.Add(gui.Node(4, 4, id: "render-only"));
            if (!renderPass) nodes.Add(gui.Node(4, 4, id: "build-only"));
            nodes.Add(gui.Node(4, 4, id: renderPass ? "y" : "x"));
            nodes.Add(gui.Node(4, 4, id: renderPass ? "x" : "y"));
            nodes.Add(gui.Node(4, 4, id: "last"));
        });

        Assert.Same(build[0], render[0]);
        Assert.Same(build[3], render[2]);
        Assert.Same(build[2], render[3]);
        Assert.Same(build[4], render[4]);
        Assert.DoesNotContain(render[1], build);
        Assert.Equal(["first", "build-only", "x", "y", "last", "render-only"], root.ChildNodes.Select(n => n.Id));
    }

    /// <summary>A node entered again later in the pass keeps matching its remaining build children.</summary>
    [Fact]
    public void ReenteredParent_ContinuesMatching()
    {
        using var harness = new FrameHarness();
        var (build, render, _) = Frame(harness, (gui, nodes) =>
        {
            var parent = gui.Node(100, 100);
            using (parent.Enter()) nodes.Add(gui.Node(4, 4));
            nodes.Add(gui.Node(4, 4));
            using (parent.Enter()) nodes.Add(gui.Node(4, 4, id: "later"));
        });

        Assert.Equal(build, render);
        Assert.Equal(2, build[0].Parent!.ChildNodes.Count);
    }

    /// <summary>Consecutive frames rebuild cleanly: the root's cursor and indexes reset with each pass.</summary>
    [Fact]
    public void ConsecutiveFrames_MatchFromTheStart()
    {
        using var harness = new FrameHarness();
        for (var frame = 0; frame < 3; frame++)
        {
            var (build, render, root) = Frame(harness, (gui, nodes) =>
            {
                if (gui.Pass == Pass.Pass2Render && frame == 1) nodes.Add(gui.Node(4, 4, id: "extra"));
                for (var i = 0; i < 3; i++) nodes.Add(gui.Node(4, 4));
            });
            Assert.Equal(build, render.Where(n => n.Id != "extra"));
            Assert.Equal(frame == 1 ? 4 : 3, root.ChildNodes.Count);
        }
    }
}
