using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Labels sit centered in the box sized around them: tab labels in their tab, tooltip and toast text in their panel.
/// <c>DrawText</c> sizes its node to the text, so the centering comes from the parent's content alignment.
/// </summary>
public class ControlTextAlignmentTests
{
    /// <summary>Every text leaf is centered on its parent, on both axes or only vertically.</summary>
    [Theory]
    [InlineData("tabs", true)]
    [InlineData("pill", true)]
    [InlineData("vertical", false)]
    [InlineData("tooltip", true)]
    [InlineData("toast", true)]
    public void TextIsCenteredInItsBox(string control, bool horizontally)
    {
        using var harness = new FrameHarness(400, 300);
        harness.Input.MoveTo(new Vector2(100, 100));
        void Draw(Gui gui)
        {
            var active = 0;
            switch (control)
            {
                case "tabs": gui.Tabs(ref active, tabs => tabs.Tab("First").Tab("Second")); break;
                case "pill": gui.PillTabs(ref active, tabs => tabs.Tab("First").Tab("Second")); break;
                case "vertical": gui.VerticalTabs(ref active, tabs => tabs.Tab("First").Tab("Second")); break;
                case "tooltip": gui.Tooltip("Tip text"); break;
                default:
                    if (gui.Pass == Pass.Pass1Build && gui.Clock.Elapsed < 0.02f)
                        gui.Toast("Saved", new ToastOptions { Duration = 100f, FadeInSeconds = 0f });
                    gui.Toasts();
                    break;
            }
        }

        for (var i = 0; i < 3; i++) harness.Frame(Draw);

        var leaves = Leaves(harness.Gui.RootNode!).ToList();
        Assert.NotEmpty(leaves);
        foreach (var text in leaves)
        {
            var box = text.Parent!.Rect;
            Assert.Equal(box.Y + box.H * 0.5f, text.Rect.Y + text.Rect.H * 0.5f, 1f);
            if (horizontally) Assert.Equal(box.X + box.W * 0.5f, text.Rect.X + text.Rect.W * 0.5f, 1f);
        }
    }

    static IEnumerable<LayoutNode> Leaves(LayoutNode node)
    {
        if (node.Children.Count == 0 && node.Parent is not null && node.Rect.W > 0 && node.Rect.H > 0)
            yield return node;
        foreach (var child in node.Children)
            foreach (var leaf in Leaves(child)) yield return leaf;
    }
}
