namespace Guinevere.Tests.Controls;

/// <summary>Round trips of the built-in color modes and shapes, within channel tolerance.</summary>
public class ColorPickerModesTests
{
    public static TheoryData<string> Colors => ["#000000ff", "#ffffffff", "#808080ff", "#ff8000ff", "#a855f780",
        "#123456ff", "#00ff7f20", "#fe01fd00"];

    /// <summary>Reading then writing every mode reproduces the color exactly, alpha included.</summary>
    [Theory]
    [MemberData(nameof(Colors))]
    public void ModesRoundTrip(string hex)
    {
        var color = Color.ParseHex(hex);
        Span<float> buffer = stackalloc float[3];
        foreach (var mode in new[]
                 {
                     ColorPickerModes.Rgb, ColorPickerModes.Hsv, ColorPickerModes.Hsl, ColorPickerModes.Linear
                 })
        {
            var value = Hsva.FromColor(color);
            var channels = buffer[..mode.Channels.Count];
            mode.Read(value, channels);
            Assert.Equal(color, mode.Write(value, channels).ToColor());
        }
    }

    /// <summary>Channel values follow each model's units.</summary>
    [Fact]
    public void ChannelsUseTheModelUnits()
    {
        var value = Hsva.FromColor(Color.ParseHex("#ff8000"));
        Span<float> channels = stackalloc float[3];
        ColorPickerModes.Rgb.Read(value, channels);
        Assert.Equal([255f, 128f, 0f], channels.ToArray());
        ColorPickerModes.Hsv.Read(value, channels);
        Assert.Equal(30f, channels[0], 0);
        Assert.Equal(100f, channels[1], 3);
        Assert.Equal(100f, channels[2], 3);
        ColorPickerModes.Hsl.Read(value, channels);
        Assert.Equal(100f, channels[1], 3);
        Assert.Equal(50f, channels[2], 3);
        ColorPickerModes.Linear.Read(value, channels);
        Assert.Equal(1f, channels[0], 3);
        Assert.Equal(0.216f, channels[1], 2);
    }

    /// <summary>Writing a gray through HSL keeps the hue the editor had.</summary>
    [Fact]
    public void HslWriteKeepsHueForGrays()
    {
        var value = new Hsva(210f, 0.4f, 0.8f);
        var gray = ColorPickerModes.Hsl.Write(value, [210f, 0f, 50f]);
        Assert.Equal(210f, gray.H);
        Assert.Equal(0f, gray.S);
        var black = ColorPickerModes.Hsl.Write(value, [210f, 50f, 0f]);
        Assert.Equal(0.4f, black.S);
    }

    /// <summary>The sRGB transfer functions invert each other.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(0.002f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void TransferFunctionsInvert(float value) =>
        Assert.Equal(value, ColorPickerModes.LinearToSrgb(ColorPickerModes.SrgbToLinear(value)), 5);

    /// <summary>Each built-in shape picks back the value under its own cursor.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ShapesPickTheirCursor(int index)
    {
        var shape = ColorPickerShapes.All[index];
        var rect = new Rect(10, 20, 200, 160);
        var value = new Hsva(200f, 0.6f, 0.7f);
        var picked = shape.Pick(rect, shape.Cursor(rect, value), value);

        Assert.Equal(value.H, picked.H, 2);
        Assert.Equal(value.S, picked.S, 3);
        Assert.Equal(value.V, picked.V, 3);
    }

    /// <summary>The wheel's center has no hue to pick and keeps the current one; empty rects keep the value.</summary>
    [Fact]
    public void WheelCenterAndEmptyRects()
    {
        var value = new Hsva(90f, 0.5f, 0.5f);
        var rect = new Rect(0, 0, 100, 100);
        Assert.Equal(value with { S = 0f }, ColorPickerShapes.Wheel.Pick(rect, new Vector2(50, 50), value));
        Assert.Equal(value, ColorPickerShapes.Wheel.Pick(default, Vector2.One, value));
        Assert.Equal(value, ColorPickerShapes.Rectangle.Pick(default, Vector2.One, value));
    }
}
