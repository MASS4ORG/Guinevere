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
            if (numeric) gui.NumberField(ref number, width: 100, height: 30, alignX: align, id: "number");
            else gui.TextInput(ref text, width: 100, height: 30, alignX: align, id: "text");
        }
        h.Frame(Draw);
        h.Click(Draw, new Vector2(150, 45));
        h.Input.PressKey(KeyboardKey.LeftControl);
        h.Input.PressKey(KeyboardKey.A);
        h.Frame(Draw);
        using var snapshot = h.Snapshot();
        using var bitmap = SKBitmap.FromImage(snapshot);
        for (var y = 30; y < 60; y++)
            for (var x = 0; x < 300; x++)
                if (x < 99 || x > 200) Assert.Equal((byte)0, bitmap.GetPixel(x, y).Alpha);
    }
}
