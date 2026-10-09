using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>Exercises scoped data lifetime, stable references and updates across complete two-pass frames.</summary>
public class GuiDataTests
{
    /// <summary>Named scopes isolate data and controls while legacy keys remain global.</summary>
    [Fact]
    public void NamedScopesIsolateValuesAndControls()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            gui.SetValue(9, "shared");
            foreach (var name in new[] { "popup", "tooltip" })
            {
                using var scope = gui.EnterDataScope(name);
                gui.SetData(name, "value");
                Assert.Equal(name, gui.GetData("", "value"));
                Assert.Equal(9, gui.GetValue(0, "shared"));
                var state = gui.ControlState("same", () => new State(name));
                Assert.Equal(name, state.Name);
                Assert.Same(state, gui.TryGetControlState<State>("same"));
                using var item = gui.ItemKey(7);
                gui.Node(10, 10, "same");
            }
            Assert.Null(gui.TryGetControlState<State>("same"));
            Assert.Equal("root", gui.GetData("root", "value"));
            using (gui.EnterDataScope("popup")) Assert.Equal("popup", gui.GetData("", "value"));
        });
        Assert.NotEqual(harness.Gui.RootNode!.ChildNodes[0].Id, harness.Gui.RootNode.ChildNodes[1].Id);
    }

    /// <summary>Setting a node context restores its parent and survives re-entry without leaking style values.</summary>
    [Fact]
    public void NodeScopesInheritAndRestoreData()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            gui.SetData(1, "count");
            var rootColor = gui.Pass == Pass.Pass1Build ? Color.Green : Color.Red;
            gui.SetTextColor(rootColor);
            var panel = gui.Node(100, 100, "panel");
            using (panel.Enter())
            {
                gui.SetDataScope("panel");
                gui.SetData(2, "count");
                gui.SetTextColor(Color.Blue);
                using (gui.Node(10, 10).Enter())
                {
                    Assert.Equal(2, gui.GetData(0, "count"));
                    Assert.Equal(Color.Blue, gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value);
                }
                gui.SetDataScope("panel");
                Assert.Equal(2, gui.GetData(0, "count"));
                using (gui.EnterDataScope("nested"))
                {
                    gui.SetData(3, "count");
                    Assert.Equal(3, gui.GetData(0, "count"));
                }
                Assert.Equal(2, gui.GetData(0, "count"));
            }
            Assert.Equal(1, gui.GetData(0, "count"));
            Assert.Equal(rootColor, gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value);
            using (panel.Enter()) Assert.Equal(2, gui.GetData(0, "count"));
            Assert.Equal(1, gui.GetData(0, "count"));
        });
    }

    /// <summary>Anonymous scopes and data follow call occurrences and keyed items through reorder and absence.</summary>
    [Fact]
    public void AutomaticDataPersistsForReappearingItems()
    {
        using var harness = new FrameHarness();
        int[] order = [1, 2];
        var values = new Dictionary<int, int>();
        var increment = true;
        void Draw(Gui gui)
        {
            foreach (var item in order)
            {
                using var key = gui.ItemKey(item);
                using var scope = gui.EnterDataScope();
                ref var value = ref gui.GetData(item);
                if (increment && gui.Pass == Pass.Pass1Build) value += 10;
                values[item] = value;
                gui.Node(10, 10);
            }
        }
        harness.Frame(Draw);
        increment = false;
        order = [2];
        harness.Frame(Draw);
        order = [2, 1, 3];
        harness.Frame(Draw);
        Assert.Equal(11, values[1]);
        Assert.Equal(12, values[2]);
        Assert.Equal(3, values[3]);
    }

    /// <summary>Data keys use typed equality and tolerate collisions without sharing unrelated contexts.</summary>
    [Fact]
    public void TypedScopeKeysAndAnonymousOccurrencesAreIndependent()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            using (gui.EnterDataScope(1)) gui.SetData("integer", "value");
            using (gui.EnterDataScope("1")) gui.SetData("string", "value");
            using (gui.EnterDataScope(new Collision(1))) gui.SetData("first", "value");
            using (gui.EnterDataScope(new Collision(2))) gui.SetData("second", "value");
            using (gui.EnterDataScope(1)) Assert.Equal("integer", gui.GetData("", "value"));
            using (gui.EnterDataScope("1")) Assert.Equal("string", gui.GetData("", "value"));
            using (gui.EnterDataScope(new Collision(1))) Assert.Equal("first", gui.GetData("", "value"));
            using (gui.EnterDataScope(new Collision(2))) Assert.Equal("second", gui.GetData("", "value"));
            for (var i = 0; i < 3; i++) Assert.Equal(i, gui.GetData(i));
            Assert.Throws<ArgumentNullException>(() => gui.EnterDataScope<string>(null!));
        });
    }

    /// <summary>References survive dictionary growth, and clearing detaches them without disposing their values.</summary>
    [Fact]
    public void ReferencesRemainStableUntilExplicitClear()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            ref var value = ref gui.GetData(1, "original");
            for (var i = 0; i < 1000; i++) gui.SetData(i, i.ToString());
            value = 42;
            Assert.Equal(42, gui.GetData(0, "original"));
            var resource = new Resource();
            gui.SetData(resource, "resource");
            gui.ClearData();
            Assert.False(resource.Disposed);
            value = 99;
            Assert.Equal(5, gui.GetData(5, "original"));
            Assert.Equal(99, value);
        });
    }

    /// <summary>Named updates run once per frame and preserve their snapshot even if a ref changes during rendering.</summary>
    [Fact]
    public void UpdatesReturnTheSameValueAcrossPasses()
    {
        using var harness = new FrameHarness();
        var updates = 0;
        var expected = 1;
        void Draw(Gui gui)
        {
            var value = gui.UpdateData(0, old => { updates++; return old + 1; }, "counter");
            Assert.Equal(expected, value);
            gui.SetData(50, "counter");
            Assert.Equal(expected, gui.UpdateData(0, _ => throw new Exception(), "counter"));
            Assert.Throws<ArgumentNullException>(() => gui.UpdateData(0, null!));
        }
        harness.Frame(Draw);
        expected = 51;
        harness.Frame(Draw);
        Assert.Equal(2, updates);
    }

    /// <summary>Scope disposal order and unclosed pass boundaries are diagnosed without corrupting the context.</summary>
    [Fact]
    public void ScopeLifetimeIsValidated()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            var outer = gui.EnterDataScope("outer");
            var inner = gui.EnterDataScope("inner");
            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            Assert.Throws<InvalidOperationException>(() => gui.SetStage(Pass.Pass2Render));
            inner.Dispose();
            using (gui.Node(10, 10).Enter()) Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            outer.Dispose();
            Assert.Throws<InvalidOperationException>(() => outer.Dispose());
            default(DataScope).Dispose();
        });
    }

    /// <summary>Warm named data access and typed scope entry do not allocate.</summary>
    [Fact]
    public void WarmDataAccessDoesNotAllocate()
    {
        using var harness = new FrameHarness();
        harness.Frame(gui =>
        {
            for (var i = 0; i < 100; i++)
            {
                using var scope = gui.EnterDataScope(123);
                gui.SetData(1, "value");
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++)
            {
                using var scope = gui.EnterDataScope(123);
                gui.SetData(gui.GetData(0, "value") + 1, "value");
            }
            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        });
    }

    /// <summary>Automatically selected node contexts stay independent across both passes and frames.</summary>
    [Fact]
    public void AutomaticNodeDataScopesResetEachPass()
    {
        using var harness = new FrameHarness();
        void Draw(Gui gui)
        {
            for (var i = 0; i < 2; i++)
            {
                using var node = gui.Node(20, 20).Enter();
                gui.SetDataScope();
                Assert.Equal(i, gui.GetData(i, "value"));
                gui.SetDataScope(i);
                Assert.Equal(i + 10, gui.GetData(i + 10, "value"));
            }
        }
        harness.Frame(Draw);
        harness.Frame(Draw);
    }

    /// <summary>Popup content and tooltip hover timers remain independent when their explicit names are scoped.</summary>
    [Fact]
    public void PopupsAndTooltipsDoNotLeakScopedState()
    {
        using var harness = new FrameHarness(600, 400);
        var open = new[] { true, false };
        var builds = new[] { 0, 0 };
        var show = false;
        var delay = 1f;
        void Draw(Gui gui)
        {
            for (var i = 0; i < 2; i++)
            {
                using var scope = gui.EnterDataScope(i);
                var index = i;
                gui.Popup(ref open[i], () =>
                {
                    ref var data = ref gui.GetData(index, "content");
                    Assert.Equal(index, data);
                    if (gui.Pass == Pass.Pass1Build) builds[index]++;
                }, position: new Vector2(i * 250, 150), closeOnClickOutside: false, id: "popup");
                var anchor = gui.Node(50, 30, "anchor").AbsoluteScreen(i * 100, 0);
                gui.Tooltip(anchor, "tip", delay, id: "tip");
                if (i == 1 && gui.Pass == Pass.Pass2Render)
                    show = gui.RootNode!.ChildNodes[^1].StyleTarget!.Value.Modifiers?.Contains("closed") != true;
            }
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(10, 10));
        harness.Frame(Draw);
        harness.Gui.Time.Update(2);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(110, 10));
        harness.Frame(Draw);
        Assert.False(show);
        delay = 0;
        open[1] = true;
        harness.Frame(Draw);
        Assert.True(show);
        Assert.True(builds[0] > builds[1]);
        Assert.Equal(1, builds[1]);
    }

    sealed record State(string Name);
    readonly record struct Collision(int Value)
    {
        public override int GetHashCode() => 0;
    }
    sealed class Resource : IDisposable
    {
        internal bool Disposed;
        public void Dispose() => Disposed = true;
    }
}
