using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

/// <summary>Checks PSS effect parsing, reuse and removal on retained nodes.</summary>
public class StyledRenderEffectsTests
{
    /// <summary>A styled filter changes pixels once and is removed with its stylesheet.</summary>
    [Fact]
    public void StyledFiltersReuseStacksAndRefreshAfterSheetRemoval()
    {
        using var frame = new FrameHarness(40, 40);
        var sheet = StyleSheet.Parse("box { width = 30; height = 30; filter = invert(1); }");
        frame.Gui.StyleSheets.Add(sheet);
        RenderEffectStack? previous = null;
        void Draw(Gui gui)
        {
            using (gui.StyledNode("box").Enter())
            {
                var stack = gui.CurrentNodeScope.Get<LayoutNodeScopeRenderEffects>().Value;
                if (previous is not null && stack is not null)
                {
                    Assert.Same(previous, stack);
                }
                previous = stack;
                gui.DrawBackgroundRect(Color.Red);
            }
        }
        frame.Frame(Draw);
        using var first = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(first);
        Assert.Equal(SKColors.Cyan, bitmap.GetPixel(10, 10));
        frame.Frame(Draw);
        frame.Gui.StyleSheets.Remove(sheet);
        frame.Frame(Draw);
        Assert.Null(previous);
    }

    /// <summary>Changing a style token rebuilds the effective filter stack.</summary>
    [Fact]
    public void FilterParametersFollowStyleTokens()
    {
        using var frame = new FrameHarness(40, 40);
        frame.Gui.StyleSheets.Add(StyleSheet.Parse("$amount = 0; box { filter = invert($amount); }"));
        static void Draw(Gui gui)
        {
            using (gui.StyledNode("box").Width(30).Height(30).Enter())
            {
                gui.DrawBackgroundRect(Color.Red);
            }
        }
        frame.Frame(Draw);
        frame.Gui.StyleSheets.SetToken("amount", "1");
        frame.Frame(Draw);
        using var image = frame.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Cyan, bitmap.GetPixel(10, 10));
    }
}
