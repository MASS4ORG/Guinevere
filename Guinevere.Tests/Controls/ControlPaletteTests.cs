namespace Guinevere.Tests.Controls;

/// <summary>Verifies that specialized Excalibur themes follow the shared palette.</summary>
public class ControlPaletteTests
{
    static readonly ControlPalette Palette = new()
    {
        BaseBackground = Color.FromArgb(255, 1, 2, 3),
        Surface = Color.FromArgb(255, 4, 5, 6),
        SurfaceHover = Color.FromArgb(255, 7, 8, 9),
        SurfaceActive = Color.FromArgb(255, 10, 11, 12),
        Border = Color.FromArgb(255, 13, 14, 15),
        Text = Color.FromArgb(255, 16, 17, 18),
        TextDim = Color.FromArgb(255, 19, 20, 21),
        Accent = Color.FromArgb(255, 22, 23, 24)
    };

    /// <summary>Docking derives every semantic color from the shared palette.</summary>
    [Fact]
    public void DockTheme_MapsSharedPalette()
    {
        var theme = DockTheme.FromPalette(Palette);

        Assert.Equal(Palette.BaseBackground, theme.TabStrip);
        Assert.Equal(Palette.Surface, theme.Tab);
        Assert.Equal(Palette.SurfaceActive, theme.Panel);
        Assert.Equal(Palette.SurfaceHover, theme.Hover);
        Assert.Equal(Palette.Border, theme.Border);
        Assert.Equal(Palette.Text, theme.Ink);
        Assert.Equal(Palette.TextDim, theme.InkDim);
        Assert.Equal(Palette.Accent, theme.Accent);
    }

    /// <summary>Standalone tab strips use the same semantic mapping as dock tabs.</summary>
    [Fact]
    public void TabStripTheme_MapsSharedPalette()
    {
        var theme = TabStripTheme.FromPalette(Palette);

        Assert.Equal(Palette.BaseBackground, theme.Strip);
        Assert.Equal(Palette.Surface, theme.Tab);
        Assert.Equal(Palette.SurfaceActive, theme.Active);
        Assert.Equal(Palette.SurfaceHover, theme.Hover);
        Assert.Equal(Palette.Text, theme.Ink);
        Assert.Equal(Palette.TextDim, theme.InkDim);
        Assert.Equal(Palette.Accent, theme.Accent);
    }

    /// <summary>Stylesheets can replace selected semantic tokens while retaining the fallback.</summary>
    [Fact]
    public void FromStyle_OverridesSemanticTokens()
    {
        var sheet = StyleSheet.Parse("control-palette { surface = #112233; focus-ring = #445566; }");
        var style = StyleResolver.Resolve([sheet], new StyleTarget("control-palette", null, []));
        var palette = ControlPalette.FromStyle(style, Palette);

        Assert.Equal(Color.FromArgb(255, 17, 34, 51), palette.Surface);
        Assert.Equal(Color.FromArgb(255, 68, 85, 102), palette.FocusRing);
        Assert.Equal(Palette.Text, palette.Text);
    }

    /// <summary>Palette values are applied independently and may be overridden by a nested scope.</summary>
    [Fact]
    public void ControlStyle_CascadesIndependentOverrides()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var gui = new Gui { Controls = ControlPalette.Dark };
        gui.BeginFrame(surface.Canvas);

        Assert.Equal(ControlPalette.Dark.Surface, gui.ControlStyle.Surface);
        Assert.Equal(ControlMetrics.FieldWidth, gui.ControlStyle.FieldWidth);

        using (gui.Node().Enter())
        {
            gui.CurrentNodeScope.Set(
            [
                ControlStyles.Value<ControlSurface, Color>(Color.Magenta),
                ControlStyles.Value<ControlFieldWidth, float>(320f)
            ]);

            Assert.Equal(Color.Magenta, gui.ControlStyle.Surface);
            Assert.Equal(320f, gui.ControlStyle.FieldWidthOr(ControlMetrics.FieldWidth));
            Assert.Equal(ControlPalette.Dark.Text, gui.ControlStyle.Text);
        }

        Assert.Equal(ControlPalette.Dark.Surface, gui.ControlStyle.Surface);
    }

    /// <summary>Existing palette objects expose their individual values for bulk application.</summary>
    [Fact]
    public void Palette_IsAStyleValueCollection()
    {
        Assert.Equal(24, Palette.Count);
        Assert.All(Palette, value => Assert.True(value.Slot >= 0));
    }

}
