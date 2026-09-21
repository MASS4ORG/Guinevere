using System.Runtime.CompilerServices;

namespace Guinevere.Tests;

public class ColorRepresentationTests
{
    [Fact]
    public void ColorUsesDeterministicRgbaLayout()
    {
        var color = Color.FromArgb(0x78, 0x12, 0x34, 0x56);

        Assert.Equal(0x12345678u, color.Rgba);
        Assert.Equal(4, Unsafe.SizeOf<Color>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<Color>());
    }

    [Theory]
    [InlineData("#ff0000", 255, 0, 0, 255)]
    [InlineData("33669980", 51, 102, 153, 128)]
    public void ColorParsesSrgbHex(string hex, byte red, byte green, byte blue, byte alpha)
    {
        var color = Color.ParseHex(hex);

        Assert.Equal(red, color.R);
        Assert.Equal(green, color.G);
        Assert.Equal(blue, color.B);
        Assert.Equal(alpha, color.A);
    }

    [Fact]
    public void ExistingColorConversionsKeepVisibleBytes()
    {
        var original = Color.FromArgb(91, 12, 128, 241);
        SKColor skia = original;

        Assert.Equal((byte)12, skia.Red);
        Assert.Equal((byte)128, skia.Green);
        Assert.Equal((byte)241, skia.Blue);
        Assert.Equal((byte)91, skia.Alpha);
    }

    [Theory]
    [InlineData("fff", 255, 255, 255, 255)]
    [InlineData("#0f08", 0, 255, 0, 136)]
    [InlineData("00ff00", 0, 255, 0, 255)]
    [InlineData("#00ff0080", 0, 255, 0, 128)]
    public void SupportsAllCssHexForms(string text, byte red, byte green, byte blue, byte alpha)
    {
        Assert.True(Color.TryParseHex(text, out var color));
        Assert.Equal((red, green, blue, alpha), (color.R, color.G, color.B, color.A));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#gggggg")]
    [InlineData("1122334455")]
    public void RejectsInvalidCssHex(string text) => Assert.False(Color.TryParseHex(text, out _));

    [Fact]
    public void AlphaCopyConstructorKeepsRgbOrder()
    {
        var color = new Color(Color.FromArgb(255, 10, 20, 30), 0.5f);

        Assert.Equal((byte)10, color.R);
        Assert.Equal((byte)20, color.G);
        Assert.Equal((byte)30, color.B);
        Assert.Equal((byte)127, color.A);
    }

    [Fact]
    public void BoxedColorEqualsColor()
    {
        object boxed = Color.Red;
        Assert.True(boxed.Equals(Color.Red));
    }

    [Fact]
    public void PackedIntegerRequiresExplicitConstruction()
    {
        var color = new Color(0x12345678u);
        Assert.Equal(((byte)0x12, (byte)0x34, (byte)0x56, (byte)0x78), (color.R, color.G, color.B, color.A));

        var integerConversions = typeof(Color).GetMethods()
            .Where(method => method.Name == "op_Implicit")
            .Where(method => method.GetParameters()[0].ParameterType is { } type
                             && (type == typeof(int) || type == typeof(uint)));
        Assert.Empty(integerConversions);
    }

    [Fact]
    public void LerpRoundsChannelMidpoints()
    {
        var midpoint = Color.Lerp(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(255, 255, 255, 255), 0.5f);
        Assert.Equal(((byte)128, (byte)128, (byte)128, (byte)128),
            (midpoint.R, midpoint.G, midpoint.B, midpoint.A));
    }
}
