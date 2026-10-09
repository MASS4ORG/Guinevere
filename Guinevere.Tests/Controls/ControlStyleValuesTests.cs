namespace Guinevere.Tests.Controls;

/// <summary>Verifies that control dimensions cascade as independent scope values.</summary>
public class ControlStyleValuesTests
{
    /// <summary>The root scope carries the default metrics, and a nested scope overrides one without the others.</summary>
    [Fact]
    public void ControlStyle_CascadesIndependentOverrides()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var gui = new Gui();
        gui.BeginFrame(surface.Canvas);

        Assert.Equal(ControlMetrics.FieldWidth, gui.ControlStyle.FieldWidth);

        using (gui.Node().Enter())
        {
            gui.CurrentNodeScope.Set(ControlStyles.Value<ControlFieldWidth, float>(320f));

            Assert.Equal(320f, gui.ControlStyle.FieldWidthOr(ControlMetrics.FieldWidth));
            Assert.Equal(ControlMetrics.FieldHeight, gui.ControlStyle.FieldHeight);
        }

        Assert.Equal(ControlMetrics.FieldWidth, gui.ControlStyle.FieldWidth);
    }
}
