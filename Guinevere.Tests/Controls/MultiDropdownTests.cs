using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Exercises controlled multi-selection through complete frames and scripted input.</summary>
public sealed class MultiDropdownTests
{
    sealed class Fixture : IDisposable
    {
        internal readonly FrameHarness Harness = new(500, 500);
        internal string[] Options = ["Alpha", "Beta", "Gamma"];
        internal IReadOnlyList<string> Selected = [];
        internal IEqualityComparer<string>? Comparer;
        internal bool Enabled = true;
        internal bool Chips;
        internal bool Mixed;
        internal readonly List<SelectionChange<string>> Changes = [];
        internal readonly List<string[]> PassNodes = [];

        internal void Draw(Gui gui)
        {
            var result = gui.MultiDropdown(Options, Selected, comparer: Comparer, width: 220, height: 24,
                maxVisibleItems: 4, enabled: Enabled, chips: Chips, mixed: Mixed,
                isMixed: option => Mixed && option == "Alpha", filePath: "multi", lineNumber: 0);
            if (result.Changed)
            {
                Selected = result.Selected;
                Changes.AddRange(result.Changes);
            }
            var nodes = Nodes(gui.RootNode!).Where(node => !node.Id.Contains("/scrollbars", StringComparison.Ordinal))
                .Select(node => node.Id);
            PassNodes.Add([.. nodes]);
        }

        internal void Frame()
        {
            PassNodes.Clear();
            Harness.Frame(Draw);
            Assert.Equal(PassNodes[0], PassNodes[1]);
        }

        internal LayoutNode? Find(string suffix) =>
            Nodes(Harness.Gui.RootNode!).FirstOrDefault(node => node.Id.EndsWith(suffix, StringComparison.Ordinal));

        internal void Click(string suffix)
        {
            Harness.Click(Draw, FrameHarness.Center(Find(suffix)!));
            Frame();
        }

        internal void Open()
        {
            Frame();
            Click("/button");
            Frame();
            Assert.NotNull(Find("/popup"));
        }

        internal void Key(KeyboardKey key)
        {
            Harness.Input.PressKey(key);
            Frame();
            Harness.Input.ReleaseKey(key);
            Frame();
        }

        internal void Search(string text)
        {
            Harness.Click(Draw, FrameHarness.Center(Find("/search")!));
            Harness.Input.PressKey(KeyboardKey.LeftControl);
            Harness.Input.PressKey(KeyboardKey.A);
            Frame();
            Harness.Input.ReleaseKey(KeyboardKey.A);
            Harness.Input.ReleaseKey(KeyboardKey.LeftControl);
            Harness.Input.TypeText(text);
            Frame();
            Frame();
        }

        public void Dispose() => Harness.Dispose();
    }

    internal static IEnumerable<LayoutNode> Nodes(LayoutNode node) => node.Children.SelectMany(Nodes).Prepend(node);

    /// <summary>Toggling keeps the popup open and delivers edits once at the frame boundary.</summary>
    [Fact]
    public void ToggleKeepsPopupOpenAndReturnsOptionOrder()
    {
        using var f = new Fixture();
        f.Open();
        f.Click("/option/2");
        f.Click("/option/0");
        Assert.Equal(["Alpha", "Gamma"], f.Selected);
        Assert.Equal(2, f.Changes.Count);
        Assert.NotNull(f.Find("/popup"));
        f.Click("/option/2");
        Assert.Equal(["Alpha"], f.Selected);
        Assert.Equal(new SelectionChange<string>("Gamma", false), f.Changes[^1]);
    }

    /// <summary>Bulk edits affect filtered rows while retaining hidden and unknown values.</summary>
    [Fact]
    public void FilteredBulkSelectionPreservesOtherValues()
    {
        using var f = new Fixture { Selected = ["outside", "Gamma"] };
        f.Open();
        f.Search("bEt");
        Assert.NotNull(f.Find("/option/1"));
        Assert.Null(f.Find("/option/0"));
        Assert.Null(f.Find("/option/2"));
        f.Click("/all");
        Assert.Equal(["Beta", "Gamma", "outside"], f.Selected);
        f.Click("/clear");
        Assert.Equal(["Gamma", "outside"], f.Selected);
        f.Search("missing");
        Assert.NotNull(f.Find("/empty"));
        f.Click("/all");
        Assert.Equal(["Gamma", "outside"], f.Selected);
    }

    /// <summary>The supplied comparer deduplicates options and selection using option order.</summary>
    [Fact]
    public void EqualityIsControlledAndExternalChangesAreObserved()
    {
        using var f = new Fixture
        {
            Options = ["alpha", "ALPHA", "beta"],
            Selected = ["BETA", "ALPHA", "unknown", "UNKNOWN"],
            Comparer = StringComparer.OrdinalIgnoreCase,
        };
        f.Open();
        Assert.Null(f.Find("/option/1"));
        f.Click("/option/0");
        Assert.Equal(["beta", "unknown"], f.Selected);
        f.Selected = ["alpha"];
        f.Frame();
        f.Click("/option/0");
        Assert.Empty(f.Selected);
    }

    /// <summary>Only viewport rows are built, including after scrolling to the end of a large list.</summary>
    [Fact]
    public void LargeListVirtualizesAndScrollsToLastOption()
    {
        using var f = new Fixture { Options = [.. Enumerable.Range(0, 10_000).Select(i => $"Option {i}")] };
        f.Open();
        Assert.InRange(Nodes(f.Harness.Gui.RootNode!).Count(), 1, 45);
        var list = f.Find("/list")!;
        f.Harness.Input.MoveTo(list.Rect.Center);
        f.Harness.Input.Scroll(-100_000);
        f.Frame();
        f.Frame();
        Assert.NotNull(f.Find("/option/9999"));
        Assert.InRange(Nodes(f.Harness.Gui.RootNode!).Count(), 1, 45);
        f.Click("/option/9999");
        Assert.Equal(["Option 9999"], f.Selected);
    }

    /// <summary>Keyboard navigation reveals virtualized rows and restores focus after dismissal.</summary>
    [Fact]
    public void KeyboardSelectionAndEscapeStayScoped()
    {
        using var f = new Fixture { Options = [.. Enumerable.Range(0, 50).Select(i => $"Option {i}")] };
        f.Open();
        f.Key(KeyboardKey.Down);
        Assert.EndsWith("/list", f.Harness.Gui.Focus.CurrentFocusedId);
        f.Key(KeyboardKey.End);
        Assert.NotNull(f.Find("/option/49"));
        Assert.EndsWith("/list", f.Harness.Gui.Focus.CurrentFocusedId);
        f.Key(KeyboardKey.Enter);
        Assert.Equal(["Option 49"], f.Selected);
        Assert.NotNull(f.Find("/option/49"));
        f.Key(KeyboardKey.Home);
        f.Key(KeyboardKey.PageDown);
        f.Key(KeyboardKey.PageUp);
        f.Key(KeyboardKey.Up);
        f.Key(KeyboardKey.Space);
        Assert.Contains("Option 0", f.Selected);
        f.Key(KeyboardKey.Escape);
        Assert.Null(f.Find("/popup"));
        Assert.EndsWith("/button", f.Harness.Gui.Focus.CurrentFocusedId);
        Assert.Null(f.Harness.Gui.Focus.ActiveScopeId);
        f.Key(KeyboardKey.Enter);
        Assert.NotNull(f.Find("/popup"));
    }

    /// <summary>Typed spaces filter options; Enter in search activates the highlighted match.</summary>
    [Fact]
    public void SearchEnterSelectsWithoutTreatingSpaceAsToggle()
    {
        using var f = new Fixture { Options = ["First option", "Second option"] };
        f.Open();
        f.Search("Second option");
        f.Key(KeyboardKey.Space);
        Assert.Empty(f.Selected);
        f.Key(KeyboardKey.Enter);
        Assert.Equal(["Second option"], f.Selected);
    }

    /// <summary>Disabled controls close, discard pending edits and stay inert.</summary>
    [Fact]
    public void DisabledDropdownIgnoresSelectionAndOpening()
    {
        using var f = new Fixture();
        f.Open();
        f.Harness.Input.MoveTo(f.Find("/option/0")!.Rect.Center);
        f.Harness.Input.PressButton();
        f.Frame();
        f.Enabled = false;
        f.Harness.Input.ReleaseButton();
        f.Frame();
        Assert.Empty(f.Selected);
        Assert.Null(f.Find("/popup"));
        f.Click("/button");
        Assert.Null(f.Find("/popup"));
    }

    /// <summary>Chip removal does not open the dropdown, and the count caps trigger node creation.</summary>
    [Fact]
    public void ChipsRemoveValuesWithoutOpening()
    {
        using var f = new Fixture { Chips = true, Selected = ["Alpha", "Beta", "Gamma"] };
        f.Frame();
        Assert.NotNull(f.Find("/chip/0"));
        Assert.NotNull(f.Find("/chip/1"));
        Assert.Null(f.Find("/chip/2"));
        f.Click("/chip/0");
        Assert.Equal(["Beta", "Gamma"], f.Selected);
        Assert.Null(f.Find("/popup"));
    }

    /// <summary>Empty options, outside clicks and separate Gui instances retain independent state.</summary>
    [Fact]
    public void EmptyPopupDismissesAndStateIsOwnedByGui()
    {
        using var first = new Fixture { Options = [] };
        using var second = new Fixture();
        first.Open();
        second.Frame();
        Assert.Null(second.Find("/popup"));
        Assert.NotNull(first.Find("/empty"));
        first.Harness.Click(first.Draw, new Vector2(490, 490));
        first.Frame();
        Assert.Null(first.Find("/popup"));
    }

    /// <summary>Indeterminate rows settle to selected when activated.</summary>
    [Fact]
    public void MixedRowsSettleToSelected()
    {
        using var f = new Fixture { Mixed = true, Selected = ["Alpha"] };
        f.Open();
        f.Click("/option/0");
        Assert.Equal(["Alpha"], f.Selected);
        Assert.Equal(new SelectionChange<string>("Alpha", true), Assert.Single(f.Changes));
    }

    /// <summary>Popups fit above a low anchor and remain usable inside a clipped panel.</summary>
    [Fact]
    public void PopupEscapesParentClippingAndOpensAboveAnchor()
    {
        using var harness = new FrameHarness(300, 300);
        IReadOnlyList<string> selected = [];
        void Draw(Gui gui)
        {
            using (gui.Node(120, 24, "panel").AbsoluteScreen(250, 270).Enter())
            {
                gui.ClipContent();
                var result = gui.MultiDropdown(["Alpha", "Beta"], selected, width: 120, height: 24,
                    filePath: "edge", lineNumber: 0);
                if (result.Changed) selected = result.Selected;
            }
        }
        harness.Frame(Draw);
        LayoutNode Find(string suffix) => Nodes(harness.Gui.RootNode!)
            .First(node => node.Id.EndsWith(suffix, StringComparison.Ordinal));
        harness.Click(Draw, new Vector2(280, 282));
        harness.Frame(Draw);
        Assert.True(Find("/popup").Rect.X <= 180);
        Assert.True(Find("/popup").Rect.Y < 270);
        harness.Click(Draw, Find("/option/1").Rect.Center);
        harness.Frame(Draw);
        Assert.Equal(["Beta"], selected);
    }
}
