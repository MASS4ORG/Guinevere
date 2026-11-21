using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Input glyphs, selections and carets stay inside their field bounds.</summary>
public sealed class NumberFieldClippingTests
{
    /// <summary>Long values cannot paint over the neighboring controls.</summary>
    [Theory]
    [InlineData(false, 0f)]
    [InlineData(false, 1f)]
    [InlineData(true, 0f)]
    [InlineData(true, 1f)]
    public void LongTextStaysInsideTheField(bool numeric, float align)
    {
        using var h = new FrameHarness(300, 100);
        var number = 123456789012345d;
        var text = "A long text value that extends well beyond the field";
        void Draw(Gui gui)
        {
            using var scope = gui.Node(100, 30, "fieldHost").Absolute(100, 30).Enter();
            if (numeric)
            {
                gui.NumberField(ref number, id: "number").AlignContentX(align).Width(100).Height(30);
            }
            else
            {
                gui.TextInput(ref text, id: "text").AlignContentX(align).Width(100).Height(30);
            }
        }
        h.Frame(Draw);
        h.Click(Draw, new Vector2(150, 45));
        h.Input.PressKey(KeyboardKey.LeftControl);
        h.Input.PressKey(KeyboardKey.A);
        h.Frame(Draw);
        using var snapshot = h.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        for (var y = 30; y < 60; y++)
        {
            for (var x = 0; x < 300; x++)
            {
                if (x < 99 || x > 200)
                {
                    Assert.Equal((byte)0, bitmap.GetPixel(x, y).Alpha);
                }
            }
        }
    }

    /// <summary>A sheet <c>align-items</c> aligns the text; without one the text stays left.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("input { align-items = right; }", true)]
    public void SheetAlignItemsAlignsTheInputText(string? sheet, bool right)
    {
        using var h = new FrameHarness(300, 100);
        if (sheet is not null)
        {
            h.Gui.StyleSheets.Add(StyleSheet.Parse(sheet));
        }

        var text = "ab";
        void Draw(Gui gui) => gui.TextInput(ref text, id: "text").Width(200);
        h.Frame(Draw);
        h.Frame(Draw);

        var field = h.Gui.RootNode!.FindChildById("text")!;
        var content = field.Children.Where(c => c.Rect.W > 0).Max(c => c.Rect.X + c.Rect.W);
        var farEdge = field.Rect.X + field.Rect.W - 20;
        Assert.Equal(right, content > farEdge);
    }
}
