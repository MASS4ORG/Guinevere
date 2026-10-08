using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Verifies splitter sizing, axis selectors, cursor styling and captured dragging.</summary>
public class SplitterTests
{
    /// <summary>The sheet sizes the divider across its axis and expands it along the other axis.</summary>
    [Theory]
    [InlineData(Axis.Horizontal, 6f, 100f)]
    [InlineData(Axis.Vertical, 200f, 6f)]
    public void DefaultSheetSizesTheAxis(Axis axis, float width, float height)
    {
        using var harness = new FrameHarness(200, 100);
        var fraction = 0.5f;
        harness.Frame(g =>
        {
            using (g.Node(200, 100).Direction(axis).Enter())
                g.Splitter(ref fraction, axis, id: "divider");
        });
        var node = Assert.Single(Assert.Single(harness.Gui.RootNode!.Children).Children);
        Assert.Equal(width, node.Rect.W);
        Assert.Equal(height, node.Rect.H);
    }

    /// <summary>Caller thickness overrides a class rule while the rule controls the cursor and live colors.</summary>
    [Theory]
    [InlineData(Axis.Horizontal)]
    [InlineData(Axis.Vertical)]
    public void ClassesAndIdsStyleLiveStates(Axis axis)
    {
        using var harness = new FrameHarness(200, 100);
        harness.Gui.Platform.Register<ICursorCapability>(harness.Input);
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            splitter.custom#divider { cursor = pointer; }
            splitter.custom#divider:horizontal { width = 12; }
            splitter.custom#divider:vertical { height = 12; }
            splitter.custom#divider:hover { background-color = #ff0000; }
            splitter.custom#divider:active { background-color = #0000ff; }
            """));
        var fraction = 0.5f;
        void Draw(Gui g)
        {
            g.DrawBackgroundRect(Color.White);
            using (g.Node(200, 100).Direction(axis).Enter())
                g.Splitter(ref fraction, axis, thickness: 8, classes: ["custom"], id: "divider");
        }
        harness.Frame(Draw);
        var node = Assert.Single(Assert.Single(harness.Gui.RootNode!.Children).Children);
        Assert.Equal(8f, axis == Axis.Horizontal ? node.Rect.W : node.Rect.H);
        harness.Input.MoveTo(new Vector2(3, 3));
        harness.Frame(Draw);
        Assert.Equal(PointerCursor.Hand, harness.Input.Cursor);
        using (var image = harness.Snapshot())
        using (var pixels = SKBitmap.FromImage(image))
            Assert.Equal(SKColors.Red, pixels.GetPixel(3, 3));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        using (var image = harness.Snapshot())
        using (var pixels = SKBitmap.FromImage(image))
            Assert.Equal(SKColors.Blue, pixels.GetPixel(3, 3));
        harness.Input.MoveTo(new Vector2(300, 200));
        harness.Frame(Draw);
        Assert.Equal(PointerCursor.Hand, harness.Input.Cursor);
        Assert.Equal(0.9f, fraction);
    }

    /// <summary>A drag reports changes once, clamps both ends and stops after release.</summary>
    [Theory]
    [InlineData(Axis.Horizontal)]
    [InlineData(Axis.Vertical)]
    public void DraggingClampsAndSettles(Axis axis)
    {
        using var harness = new FrameHarness(200, 100);
        var fraction = 0.5f;
        var changed = false;
        void Draw(Gui g)
        {
            using (g.Node(200, 100).Direction(axis).Enter())
                changed |= g.Splitter(ref fraction, axis, id: "divider");
        }
        harness.Frame(Draw);
        Assert.False(changed);
        harness.Input.MoveTo(new Vector2(3, 3));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(-200, -100));
        harness.Frame(Draw);
        Assert.True(changed);
        Assert.Equal(0.1f, fraction);
        changed = false;
        harness.Frame(Draw);
        Assert.False(changed);
        harness.Input.ReleaseButton(MouseButton.Left);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(150, 80));
        harness.Frame(Draw);
        Assert.False(changed);
        Assert.Equal(0.1f, fraction);
    }

    /// <summary>A divider inside a zero-length container cannot change its fraction.</summary>
    [Fact]
    public void EmptyContainerDoesNotDivideByZero()
    {
        using var harness = new FrameHarness(200, 100);
        var fraction = 0.5f;
        void Draw(Gui g)
        {
            using (g.Node().Width(UnitValue.Pixels(0)).Height(100).Direction(Axis.Horizontal).Enter())
                g.Splitter(ref fraction, Axis.Horizontal, id: "divider");
        }
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(3, 3));
        harness.Input.PressButton(MouseButton.Left);
        harness.Frame(Draw);
        harness.Input.MoveTo(new Vector2(100, 3));
        harness.Frame(Draw);
        Assert.Equal(0.5f, fraction);
    }
}
