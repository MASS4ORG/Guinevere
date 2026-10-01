using MASS4.Attributes;

namespace Guinevere.Tests.Forms;

public class AttributeTests
{
    [Fact]
    public void TextAreaKeepsAtLeastOneLineAndMaxNotBelowMin()
    {
        var defaults = new TextAreaAttribute();
        var inverted = new TextAreaAttribute(5, 2);
        var degenerate = new TextAreaAttribute(0, 0);

        Assert.Equal((3, 10), (defaults.MinLines, defaults.MaxLines));
        Assert.Equal((5, 5), (inverted.MinLines, inverted.MaxLines));
        Assert.Equal((1, 1), (degenerate.MinLines, degenerate.MaxLines));
    }

    [Fact]
    public void ListDrawerSettingsDefaults()
    {
        var settings = new ListDrawerSettingsAttribute();

        Assert.Equal(10, settings.NumberOfItemsPerPage);
        Assert.True(settings.DraggableItems);
        Assert.False(settings.ShowIndexLabels);
        Assert.True(settings.ShowPaging);
    }

    [Theory]
    [InlineData("#FF8000", 1f, 128 / 255f, 0f, 1f)]
    [InlineData("FF800040", 1f, 128 / 255f, 0f, 64 / 255f)]
    [InlineData("#XYZ", 1f, 0f, 1f, 1f)]
    [InlineData("#12345", 1f, 0f, 1f, 1f)]
    [InlineData(null, 1f, 0f, 1f, 1f)]
    public void GUIColorParsesHexOrFallsBackToMagenta(string? hex, float r, float g, float b, float a)
    {
        var color = new GUIColorAttribute(hex!);

        Assert.Equal((r, g, b, a), (color.R, color.G, color.B, color.A));
    }

    [Fact]
    public void GUIColorKeepsChannels()
    {
        var color = new GUIColorAttribute(0.1f, 0.2f, 0.3f);

        Assert.Equal((0.1f, 0.2f, 0.3f, 1f), (color.R, color.G, color.B, color.A));
    }

    [Fact]
    public void TitleAndRequiredKeepTheirArguments()
    {
        var title = new TitleAttribute("Audio");
        var custom = new TitleAttribute("Audio", "Mixer", bold: false, horizontalLine: false);

        Assert.Equal(("Audio", null, true, true), (title.Title, title.Subtitle, title.Bold, title.HorizontalLine));
        Assert.Equal(("Mixer", false, false), (custom.Subtitle, custom.Bold, custom.HorizontalLine));
        Assert.Null(new RequiredAttribute().Message);
        Assert.Equal("Pick one", new RequiredAttribute("Pick one").Message);
    }
}
