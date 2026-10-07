namespace Guinevere.Tests.Styling;

/// <summary>Tests for the CSS-shaped visual value parsers in <see cref="StyleBoxValues"/>.</summary>
public class StyleBoxValuesTests
{
    static ResolvedStyle Style(string declarations) =>
        new Gui().With(StyleSheet.Parse($"box {{ {declarations} }}")).ResolveStyle("box");

    /// <summary>Splitting ignores separators inside parentheses and quotes and drops empty parts.</summary>
    [Fact]
    public void Split_RespectsNesting()
    {
        Assert.Equal(["a", "f(b, c)", "'d,e'"], StyleBoxValues.Split("a, f(b, c),, 'd,e'", ','));
        Assert.Equal(["0", "2px", "rgb(1, 2, 3)"], StyleBoxValues.Split(" 0  2px\trgb(1, 2, 3) ", ' '));
        Assert.Equal(["'open"], StyleBoxValues.Split("'open", ','));
    }

    /// <summary>Shadow layers keep their order; malformed layers are skipped.</summary>
    [Fact]
    public void Shadows_ParseLayers()
    {
        var shadows = StyleBoxValues.Shadows("0 2px 8px 1px #ff000080, inset 1 2 #00ff00, 3 4, x y, 1, 1 2 3 4 5");

        Assert.Equal(
        [
            new BoxShadow(false, 0, 2, 8, 1, Color.FromArgb(128, 255, 0, 0)),
            new BoxShadow(true, 1, 2, 0, 0, Color.FromArgb(255, 0, 255, 0)),
            new BoxShadow(false, 3, 4, 0, 0, Color.Black),
        ], shadows);
        Assert.Empty(StyleBoxValues.Shadows("none"));
        Assert.Empty(StyleBoxValues.Shadows(null));
    }

    /// <summary>Radii follow the CSS shorthand order; percentages use the shorter side.</summary>
    [Theory]
    [InlineData("4", 4, 4, 4, 4)]
    [InlineData("4 8", 4, 8, 4, 8)]
    [InlineData("4 8 2", 4, 8, 2, 8)]
    [InlineData("1 2 3 4", 1, 2, 3, 4)]
    [InlineData("50%", 20, 20, 20, 20)]
    [InlineData("6 / 2", 6, 6, 6, 6)]
    [InlineData("1 2 3 4 5", 0, 0, 0, 0)]
    [InlineData("big", 0, 0, 0, 0)]
    [InlineData(null, 0, 0, 0, 0)]
    public void Radii_FollowShorthand(string? value, float tl, float tr, float br, float bl)
    {
        var radii = StyleBoxValues.Radii(value, new Rect(0, 0, 100, 40));

        Assert.Equal([tl, tr, br, bl], radii.Select(r => r.X));
    }

    /// <summary>The outline shorthand and longhands combine; missing colors or <c>none</c> draw nothing.</summary>
    [Fact]
    public void Outline_CombinesShorthandAndLonghands()
    {
        Assert.Equal(new BoxOutline(2, 0, Color.FromArgb(255, 0, 0, 255)),
            StyleBoxValues.Outline(Style("outline = 2px solid #0000ff;")));
        Assert.Equal(new BoxOutline(4, 3, Color.FromArgb(255, 255, 0, 0)),
            StyleBoxValues.Outline(Style("outline = 2px #0000ff; outline-width = 4; outline-color = #ff0000; outline-offset = 3;")));
        Assert.Equal(3f, StyleBoxValues.Outline(Style("outline-color = #ff0000;"))!.Value.Width);
        Assert.Null(StyleBoxValues.Outline(Style("outline = none;")));
        Assert.Null(StyleBoxValues.Outline(Style("outline = 2px solid;")));
        Assert.Null(StyleBoxValues.Outline(Style("outline = 0 #ff0000;")));
        Assert.Null(StyleBoxValues.Outline(Style("width = 1;")));
    }

    /// <summary>Gradients accept angles in several units, side keywords, stop positions and evaluated colors.</summary>
    [Theory]
    [InlineData("linear-gradient(#000000, #ffffff)", true)]
    [InlineData("linear-gradient(90deg, #000000, #ffffff 80%)", true)]
    [InlineData("linear-gradient(0.25turn, red, blue)", true)]
    [InlineData("linear-gradient(1.5rad, red, blue)", true)]
    [InlineData("linear-gradient(to top right, red, mix(red, blue, 0.5), blue)", true)]
    [InlineData("linear-gradient(to left, red, blue)", true)]
    [InlineData("linear-gradient(to top, red, blue)", true)]
    [InlineData("linear-gradient(to bottom left, red, blue)", true)]
    [InlineData("linear-gradient(to top left, red, blue)", true)]
    [InlineData("linear-gradient(to bottom right, red, blue)", true)]
    [InlineData("linear-gradient(to bottom, red, blue)", true)]
    [InlineData("linear-gradient(to right, red, blue)", true)]
    [InlineData("linear-gradient(90deg, red)", false)]
    [InlineData("linear-gradient(red, nope)", false)]
    [InlineData("linear-gradient(red, mix(red))", false)]
    [InlineData("linear-gradient(red, blue 50% extra)", false)]
    [InlineData("linear-gradient(red, blue", false)]
    [InlineData("radial-gradient(red, blue)", false)]
    [InlineData(null, false)]
    public void Gradient_Parses(string? value, bool valid) =>
        Assert.Equal(valid, StyleBoxValues.Gradient(value, new SKRect(0, 0, 10, 10)) is not null);

    /// <summary>CSS cursor keywords map to the closest platform cursor.</summary>
    [Theory]
    [InlineData("auto", PointerCursor.Default)]
    [InlineData("default", PointerCursor.Default)]
    [InlineData("arrow", PointerCursor.Arrow)]
    [InlineData("TEXT", PointerCursor.Text)]
    [InlineData("pointer", PointerCursor.Hand)]
    [InlineData("hand", PointerCursor.Hand)]
    [InlineData("crosshair", PointerCursor.Crosshair)]
    [InlineData("ew-resize", PointerCursor.ResizeHorizontal)]
    [InlineData("col-resize", PointerCursor.ResizeHorizontal)]
    [InlineData("ns-resize", PointerCursor.ResizeVertical)]
    [InlineData("row-resize", PointerCursor.ResizeVertical)]
    [InlineData("nwse-resize", PointerCursor.ResizeDiagonalNorthWestSouthEast)]
    [InlineData("nesw-resize", PointerCursor.ResizeDiagonalNorthEastSouthWest)]
    [InlineData("not-allowed", PointerCursor.NotAllowed)]
    [InlineData("move", PointerCursor.Move)]
    [InlineData("grabbing", PointerCursor.Move)]
    public void Cursor_MapsKeywords(string value, PointerCursor expected) =>
        Assert.Equal(expected, StyleBoxValues.Cursor(value));

    /// <summary>Unknown cursors and opacities are ignored; opacities clamp to 0..1.</summary>
    [Fact]
    public void CursorAndOpacity_Fallbacks()
    {
        Assert.Null(StyleBoxValues.Cursor("zoom-in"));
        Assert.Null(StyleBoxValues.Cursor(null));
        Assert.Equal(0.5f, StyleBoxValues.Opacity("50%"));
        Assert.Equal(1f, StyleBoxValues.Opacity("3"));
        Assert.Null(StyleBoxValues.Opacity("half"));
    }

    /// <summary>Font weights and styles follow CSS keywords.</summary>
    [Theory]
    [InlineData(null, 400)]
    [InlineData("normal", 400)]
    [InlineData("bold", 700)]
    [InlineData("300", 300)]
    [InlineData("5000", 1000)]
    [InlineData("heavy", 400)]
    public void FontWeight_Parses(string? value, int expected) => Assert.Equal(expected, StyleFonts.Weight(value));

    /// <summary>Only <c>italic</c> and <c>oblique</c> slant the face.</summary>
    [Fact]
    public void FontStyle_Parses()
    {
        Assert.True(StyleFonts.Italic("Italic"));
        Assert.True(StyleFonts.Italic("oblique"));
        Assert.False(StyleFonts.Italic("normal"));
        Assert.False(StyleFonts.Italic(null));
    }
}

static class GuiSheetExtensions
{
    /// <summary>Adds a sheet and returns the GUI, for one-line test setup.</summary>
    public static Gui With(this Gui gui, StyleSheet sheet)
    {
        gui.StyleSheets.Add(sheet);
        return gui;
    }
}
