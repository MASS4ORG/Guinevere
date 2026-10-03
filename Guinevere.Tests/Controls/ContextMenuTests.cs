using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Context menus share cascade navigation and retain their opening position.</summary>
public class ContextMenuTests
{
    /// <summary>Checks context anchoring, submenu travel and action dismissal.</summary>
    [Fact]
    public void DefaultAnchorStaysAtTheOpeningPointerAndNestedActionsCloseTheMenu()
    {
        using var h = new FrameHarness(width: 700);
        var open = true;
        var runs = 0;
        void Draw(Gui gui) => gui.ContextMenu(ref open, menu => menu
            .Item("Off", () => runs += 10, enabled: false)
            .Separator()
            .Submenu("More", sub => sub.Item("Run", () => runs++)));
        h.Input.MoveTo(new Vector2(30, 40));
        h.Frame(Draw);
        var root = h.Gui.RootNode!.Children[0];
        Assert.Equal(new Vector2(30, 40), root.Rect.Position);
        h.Click(Draw, root.Children[0].Center);
        Assert.Equal(0, runs);
        h.Input.MoveTo(root.Children[2].Center);
        h.Frame(Draw);
        var child = h.Gui.RootNode!.Children[1];
        Assert.Equal(new Vector2(30, 40), h.Gui.RootNode.Children[0].Rect.Position);
        h.Input.MoveTo(root.Children[1].Center);
        h.Frame(Draw);
        Assert.Equal(2, h.Gui.RootNode.Children.Count);
        h.Click(Draw, child.Children[0].Center);
        Assert.Equal(1, runs);
        Assert.False(open);
        h.Frame(Draw);
        Assert.Empty(h.Gui.RootNode.Children);
    }
}
