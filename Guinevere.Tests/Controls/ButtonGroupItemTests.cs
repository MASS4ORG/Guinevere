namespace Guinevere.Tests.Controls;

/// <summary>A button group item converts from text, an icon or content and defaults to enabled without an id.</summary>
public sealed class ButtonGroupItemTests
{
    [Fact]
    public void ConvertsFromTextIconAndContent()
    {
        var icon = Icon.FromGlyph("+").Labeled("Add");
        ButtonGroupItem text = "Left";
        ButtonGroupItem picture = icon;
        ButtonGroupItem content = Content.Row(icon, "Both");
        ButtonGroupItem none = (string?)null;

        Assert.Equal(("Left", true, null), (text.Content.Label, text.Enabled, text.Id));
        Assert.Equal(ContentKind.Icon, picture.Content.Kind);
        Assert.Equal(ContentKind.Row, content.Content.Kind);
        Assert.True(none.Content.IsEmpty);
    }
}
