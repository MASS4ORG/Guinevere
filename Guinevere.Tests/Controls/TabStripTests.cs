using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Checks the tab strip's selection, close and title behaviour across full frames.</summary>
public class TabStripTests
{
    /// <summary>Picking an inactive tab moves the active key and raises <c>Changed</c> on that frame only.</summary>
    [Fact]
    public void ClickingAnInactiveTabSetsTheActiveKeyAndChangesOnlyThatFrame()
    {
        using var h = new FrameHarness();
        var active = "one";
        var changed = false;
        void Draw(Gui gui)
        {
            var strip = gui.TabStrip(ref active, tabs => tabs.Tab("One", key: "one").Tab("Two", key: "two"),
                id: "tabs");
            changed |= strip.Changed;
        }

        h.Frame(Draw);
        var two = h.Gui.RootNode!.FindChildById("tabs/two")!;
        h.Input.MoveTo(FrameHarness.Center(two));
        h.Frame(Draw);

        h.Input.PressButton();
        changed = false;
        h.Frame(Draw);
        Assert.Equal("two", active);
        Assert.True(changed);

        h.Input.ReleaseButton();
        changed = false;
        h.Frame(Draw);
        Assert.Equal("two", active);
        Assert.False(changed);
    }

    /// <summary>A closable tab with an <c>onClose</c> calls it once and stays built, so the caller removes it.</summary>
    [Fact]
    public void ClosingATabWithOnCloseCallsItOnceAndLeavesItBuilt()
    {
        using var h = new FrameHarness();
        var active = "one";
        var closes = 0;
        void Draw(Gui gui) => gui.TabStrip(ref active, tabs => tabs
                .Tab("One", key: "one")
                .Tab("Two", key: "two", closable: true, onClose: () => closes++),
            id: "tabs");

        h.Frame(Draw);
        var close = h.Gui.RootNode!.FindChildById("tabs/two/close")!;
        h.Click(Draw, FrameHarness.Center(close));

        Assert.Equal(1, closes);
        Assert.Equal("one", active);
        Assert.NotNull(h.Gui.RootNode!.FindChildById("tabs/two"));
    }

    /// <summary>Without an <c>onClose</c> the strip hides the closed tab itself.</summary>
    [Fact]
    public void ClosingATabWithoutOnCloseHidesIt()
    {
        using var h = new FrameHarness();
        var active = "one";
        void Draw(Gui gui) => gui.TabStrip(ref active, tabs => tabs
                .Tab("One", key: "one")
                .Tab("Two", key: "two", closable: true),
            id: "tabs");

        h.Frame(Draw);
        var close = h.Gui.RootNode!.FindChildById("tabs/two/close")!;
        h.Click(Draw, FrameHarness.Center(close));

        Assert.Null(h.Gui.RootNode!.FindChildById("tabs/two"));
        Assert.NotNull(h.Gui.RootNode!.FindChildById("tabs/one"));
        Assert.Equal("one", active);
    }

    /// <summary>A row title with an icon builds, and the tab it sits in is wider than the same text alone.</summary>
    [Fact]
    public void ARowTitleWithAnIconBuildsAndWidensTheTab()
    {
        var plain = TabWidth(static tabs => tabs.Tab("Text", key: "text"));
        var withIcon = TabWidth(static tabs => tabs.Tab(Content.Row(Icon.FromGlyph("+"), "Text"), key: "text"));

        Assert.True(withIcon > plain, $"icon tab {withIcon} should be wider than text-only tab {plain}");
    }

    static float TabWidth(Action<TabBuilder> build)
    {
        using var h = new FrameHarness();
        var active = "text";
        void Draw(Gui gui) => gui.TabStrip(ref active, build, id: "tabs");

        h.Frame(Draw);
        var tab = h.Gui.RootNode!.FindChildById("tabs/text");
        Assert.NotNull(tab);
        return tab.Rect.W;
    }
}
