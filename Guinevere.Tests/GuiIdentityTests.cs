using Autoformers;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Exercises automatic identities through real build/render passes and persistent input state.</summary>
public class GuiIdentityTests
{
    /// <summary>Loop occurrences are distinct, reuse the build node in render, and survive across frames.</summary>
    [Fact]
    public void LoopNodesMatchAcrossPassesAndFrames()
    {
        using var harness = new FrameHarness();
        var build = new List<LayoutNode>();
        var ids = new List<int>();
        void Draw(Gui gui)
        {
            for (var i = 0; i < 4; i++)
            {
                var node = gui.Node(20, 20);
                if (gui.Pass == Pass.Pass1Build) build.Add(node);
                else Assert.Same(build[i], node);
            }
            Assert.Equal(4, gui.CurrentNode.Pass2NodeCount);
        }
        harness.Frame(Draw);
        ids.AddRange(build.Select(node => node.Identity));
        Assert.Equal(4, ids.Distinct().Count());
        build.Clear();
        harness.Frame(Draw);
        Assert.Equal(ids, build.Select(node => node.Identity));
        Assert.Equal(4, harness.Gui.RootNode!.ChildNodes.Count);
    }

    /// <summary>A conditional sibling at a different call site cannot move the identity of a later node.</summary>
    [Fact]
    public void ConditionalSiblingDoesNotShiftAnotherCallSite()
    {
        using var harness = new FrameHarness();
        var show = false;
        LayoutNode? target = null;
        void Draw(Gui gui)
        {
            if (show) gui.Node(20, 20);
            target = gui.Node(20, 20);
        }
        harness.Frame(Draw);
        var identity = target!.Identity;
        var name = target.Id;
        show = true;
        harness.Frame(Draw);
        Assert.Equal(identity, target!.Identity);
        Assert.Same(name, target.Id);
        Assert.Equal(2, harness.Gui.RootNode!.ChildNodes.Count);
    }

    /// <summary>Stable typed keys preserve state after insertion, removal and reorder, including nested children.</summary>
    [Fact]
    public void KeyedItemsPreserveStateThroughCollectionChanges()
    {
        using var harness = new FrameHarness();
        int[] items = [1, 2, 3];
        var nodes = new Dictionary<int, LayoutNode>();
        void Draw(Gui gui)
        {
            foreach (var item in items)
            {
                using var key = gui.ItemKey(item);
                using var row = gui.Node(80, 30).Enter();
                var child = gui.Node(20, 20);
                nodes[item] = child;
                var state = gui.ControlState(child.Id, () => new State { Value = item });
                Assert.Equal(item, state.Value);
            }
        }
        harness.Frame(Draw);
        var original = nodes.ToDictionary(pair => pair.Key, pair => (pair.Value.Identity, pair.Value.Id));
        items = [3, 4, 1];
        harness.Frame(Draw);
        foreach (var item in new[] { 1, 3 })
        {
            Assert.Equal(original[item].Identity, nodes[item].Identity);
            Assert.Same(original[item].Id, nodes[item].Id);
        }
        items = [2, 1, 3];
        harness.Frame(Draw);
        Assert.Equal(original[2].Identity, nodes[2].Identity);
    }

    /// <summary>Selector names can repeat across keyed items while internal identities and focus remain separate.</summary>
    [Fact]
    public void SemanticStyleIdsRemainIndependentOfItemIdentity()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("#editor { width = 83px; height = 24px; }"));
        var nodes = new Dictionary<int, LayoutNode>();
        void Draw(Gui gui)
        {
            for (var i = 0; i < 2; i++)
            {
                using var key = gui.ItemKey(i);
                var node = gui.StyledNode("input", id: "editor");
                nodes[i] = node;
                using (node.Enter()) gui.RegisterFocusable();
            }
        }
        harness.Frame(Draw);
        Assert.NotEqual(nodes[0].Id, nodes[1].Id);
        Assert.NotEqual(nodes[0].Identity, nodes[1].Identity);
        Assert.All(nodes.Values, node => Assert.Equal(83, node.Rect.W));
        Assert.All(nodes.Values, node => Assert.Equal("editor", node.StyleTarget!.Value.Id));
        harness.Gui.RequestFocus(nodes[1].Id);
        harness.Frame(Draw);
        Assert.True(harness.Gui.HasFocus(nodes[1].Id));
        Assert.False(harness.Gui.HasFocus(nodes[0].Id));
    }

    /// <summary>Text fields in a loop have independent edit buffers and keep focus on their item after reorder.</summary>
    [Fact]
    public void AutomaticTextInputStateAndFocusFollowKeyedItem()
    {
        using var harness = new FrameHarness();
        var values = new[] { "first", "second" };
        int[] order = [0, 1];
        void Draw(Gui gui)
        {
            foreach (var item in order)
            {
                using var key = gui.ItemKey(item);
                gui.TextInput(ref values[item], width: 100, height: 30);
            }
        }
        harness.Frame(Draw);
        var children = harness.Gui.RootNode!.ChildNodes;
        var first = children[0].Id;
        var second = children[1].Id;
        Assert.NotSame(harness.Gui.TryGetControlState<TextEditState>(first),
            harness.Gui.TryGetControlState<TextEditState>(second));
        harness.Gui.RequestFocus(second);
        harness.Frame(Draw);
        order = [1, 0];
        harness.Frame(Draw);
        Assert.Equal(second, harness.Gui.RootNode.ChildNodes[0].Id);
        Assert.True(harness.Gui.HasFocus(second));
        harness.Input.TypeText("!");
        harness.Frame(Draw);
        Assert.Equal("first", values[0]);
        Assert.Contains("!", values[1]);
    }

    /// <summary>Unkeyed ordinary fields no longer share one edit buffer when no ID is supplied.</summary>
    [Fact]
    public void UnnamedTextInputsInLoopDoNotShareState()
    {
        using var harness = new FrameHarness();
        var values = new[] { "one", "two" };
        void Draw(Gui gui)
        {
            for (var i = 0; i < values.Length; i++) gui.TextInput(ref values[i]);
        }
        harness.Frame(Draw);
        var nodes = harness.Gui.RootNode!.ChildNodes;
        var first = harness.Gui.TryGetControlState<TextEditState>(nodes[0].Id);
        var second = harness.Gui.TryGetControlState<TextEditState>(nodes[1].Id);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.Equal("one", first.Text);
        Assert.Equal("two", second.Text);
    }

    /// <summary>Pointer capture stays attached to a keyed row across reorder during a gesture.</summary>
    [Fact]
    public void PointerCaptureFollowsKeyedItem()
    {
        using var harness = new FrameHarness();
        int[] items = [1, 2];
        var dragged = new List<int>();
        void Draw(Gui gui)
        {
            foreach (var item in items)
            {
                using var key = gui.ItemKey(item);
                using var row = gui.Node(100, 30).Enter();
                if (gui.GetInteractable().OnDrag(out _)) dragged.Add(item);
            }
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(FrameHarness.Center(harness.Gui.RootNode!.ChildNodes[0]));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        var captured = harness.Gui.PointerCapture;
        Assert.NotNull(captured);
        items = [2, 1];
        dragged.Clear();
        harness.Input.MoveTo(new Vector2(150, 150));
        harness.Frame(Draw);
        Assert.Equal(captured, harness.Gui.PointerCapture);
        Assert.Contains(1, dragged);
        Assert.DoesNotContain(2, dragged);
        harness.Input.ReleaseButton(MouseButton.Left);
        harness.Frame(Draw);
        Assert.False(harness.Gui.IsPointerCaptured);
    }

    /// <summary>Duplicate explicit siblings and duplicate keyed items are diagnosed during either pass.</summary>
    [Fact]
    public void DuplicateKeysAndIdsAreRejected()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            gui.Node(id: "same");
            Assert.Throws<InvalidOperationException>(() => gui.Node(id: "same"));
            using (gui.ItemKey(1)) gui.Node();
            Assert.Throws<InvalidOperationException>(() => gui.ItemKey(1));
        });
    }

    /// <summary>Nested item scopes restore identity and reject disposal or pass changes out of order.</summary>
    [Fact]
    public void ItemScopesRestoreIdentityAndValidateLifetime()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            var before = gui.NodeId("site", 1);
            var outer = gui.ItemKey(1);
            var scoped = gui.NodeId("site", 1);
            var inner = gui.ItemKey(1);
            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            Assert.Throws<InvalidOperationException>(() => gui.SetStage(Pass.Pass2Render));
            inner.Dispose();
            Assert.Equal(scoped, gui.NodeId("site", 1));
            outer.Dispose();
            Assert.Same(before, gui.NodeId("site", 1));
            default(ItemKeyScope).Dispose();
        });
    }

    /// <summary>Both sizing overloads, retained construction and append use the same numeric identity rules.</summary>
    [Fact]
    public void NodeOverloadsAndRetainedConstructionUseIdentity()
    {
        using var harness = new FrameHarness();
        var build = new Dictionary<string, LayoutNode>();
        harness.Frame(gui =>
        {
            var pixels = gui.Node(20.5f, 10.5f, id: "pixels");
            var units = gui.Node(UnitValue.Pixels(20), UnitValue.Pixels(10), id: "units");
            if (gui.Pass == Pass.Pass1Build)
            {
                build["pixels"] = pixels;
                build["units"] = units;
            }
            else
            {
                Assert.Same(build["pixels"], pixels);
                Assert.Same(build["units"], units);
            }
        });
        var parent = LayoutNode.CreateRoot(harness.Gui, 100, 100);
        var retained = new LayoutNode(null, harness.Gui, parent);
        var first = parent.AppendNode();
        var second = parent.AppendNode();
        Assert.NotEqual(retained.Identity, first.Identity);
        Assert.NotEqual(first.Identity, second.Identity);
        Assert.NotEmpty(retained.Id);
    }

    /// <summary>Unnamed numeric fields keep separate edit state and only commit to the focused instance.</summary>
    [Fact]
    public void UnnamedNumberFieldsInLoopCommitIndependently()
    {
        using var harness = new FrameHarness();
        var values = new[] { 1d, 2d };
        void Draw(Gui gui)
        {
            for (var i = 0; i < values.Length; i++) gui.NumberField(ref values[i]);
        }
        harness.Frame(Draw);
        var second = harness.Gui.RootNode!.ChildNodes[1].Id;
        harness.Gui.RequestFocus(second);
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.LeftControl);
        harness.Input.PressKey(KeyboardKey.A);
        harness.Frame(Draw);
        harness.Input.ReleaseKey(KeyboardKey.A);
        harness.Input.ReleaseKey(KeyboardKey.LeftControl);
        harness.Input.TypeText("8");
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.Enter);
        harness.Frame(Draw);
        Assert.Equal(1, values[0]);
        Assert.Equal(8, values[1]);
    }

    /// <summary>Forms and individual field APIs supply identity without requiring caller-made strings.</summary>
    [Fact]
    public void FormsAndStandaloneFieldsUseAutomaticIdentity()
    {
        using var harness = new FrameHarness(800, 1200);
        var models = new[] { FormBuilder.Build(new State { Value = 1 }), FormBuilder.Build(new State { Value = 2 }) };
        var ids = new List<string>();
        void Draw(Gui gui)
        {
            foreach (var model in models) gui.Form(model);
            foreach (var model in models) gui.FormField(model.Sections[0].Fields[0]);
            foreach (var model in models) gui.FormFieldEditor(model.Sections[0].Fields[0]);
            if (gui.Pass == Pass.Pass2Render)
            {
                var names = gui.RootNode!.ChildNodes.Select(node => node.Id).ToArray();
                Assert.Equal(names.Length, names.Distinct().Count());
                if (ids.Count == 0) ids.AddRange(names);
                else Assert.Equal(ids, names);
            }
        }
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.NotEmpty(ids);
    }

    /// <summary>Value-returning input wrappers forward the original call site and keep separate identities.</summary>
    [Fact]
    public void InputWrappersForwardCallerIdentity()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            gui.TextInput("plain");
            gui.PasswordInput("secret");
            gui.TextArea("multiline");
            var number = 2f;
            gui.NumberField(ref number);
        });
        var nodes = harness.Gui.RootNode!.ChildNodes;
        Assert.Equal(4, nodes.Select(node => node.Identity).Distinct().Count());
        Assert.All(nodes, node => Assert.Contains(nameof(GuiIdentityTests), node.Id));
    }

    /// <summary>Automatic tabs in a loop keep activation requests separate and apply them on the next build pass.</summary>
    [Fact]
    public void AutomaticTabsKeepPendingSelectionIndependent()
    {
        using var harness = new FrameHarness(400, 400);
        var selected = new[] { 0, 0 };
        void Draw(Gui gui)
        {
            for (var i = 0; i < selected.Length; i++)
            {
                using var item = gui.ItemKey(i);
                using var panel = gui.Node(200, 150).Enter();
                gui.Tabs(ref selected[i], tabs => tabs.Tab("A").Tab("B"));
            }
        }
        harness.Frame(Draw);
        var secondPanel = harness.Gui.RootNode!.ChildNodes[1];
        var secondTab = secondPanel.ChildNodes[0].ChildNodes[0].ChildNodes[1];
        harness.Click(Draw, FrameHarness.Center(secondTab));
        harness.Frame(Draw);
        Assert.Equal(0, selected[0]);
        Assert.Equal(1, selected[1]);
        harness.Gui.RequestFocus(secondTab.Id);
        harness.Frame(Draw);
        harness.Input.PressKey(KeyboardKey.Left);
        harness.Frame(Draw);
        harness.Input.ReleaseKey(KeyboardKey.Left);
        harness.Frame(Draw);
        Assert.Equal(0, selected[0]);
        Assert.Equal(0, selected[1]);
    }

    sealed class State
    {
        public int Value;
    }
}
