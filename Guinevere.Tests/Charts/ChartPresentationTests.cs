using Guinevere.Charts;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Charts;

/// <summary>Checks measured labels and chart parts resolved through the PSS cascade.</summary>
public sealed class ChartPresentationTests
{
    /// <summary>PSS selectors on chart nodes style drawn parts and small labels have deterministic fallbacks.</summary>
    [Fact]
    public void PssStylesPartsAndLabelsOnlyDrawWhenTheyFit()
    {
        using var harness = new FrameHarness(260, 160);
        harness.Gui.ConfigureFonts(Font.FromFamilyName("sans-serif", 14));
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("""
            chart#metrics chart-axis { border-color = #ff0000; }
            chart#metrics chart-grid { border-color = #0000ff; }
            chart#metrics chart-tooltip { color = #00ff00; background-color = #000000; }
            """));
        harness.Frame(static gui =>
        {
            using var chart = gui.StyledNode("chart", id: "metrics").Expand().Enter();
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }
            var style = ChartStyle.Resolve(gui);
            Assert.Equal(Color.Red, style.Axis);
            Assert.Equal(Color.Blue, style.Grid);
            Assert.Equal(Color.Lime, style.TooltipText);
            Assert.Equal(Color.Black, style.TooltipBackground);

            var area = new Rect(10, 10, 220, 130);
            var plot = new CartesianPlot(new Rect(25, 25, 150, 60), new ChartRange(0, 10), new ChartRange(0, 1));
            Assert.True(ChartLabels.Draw(gui, area, "A label", style.Label, size: 12));
            Assert.False(ChartLabels.Draw(gui, new Rect(10, 10, 1, 1), "A label", style.Label));
            Assert.True(ChartLabels.DrawTooltip(gui, area, new Vector2(225, 135), "Near the edge", style));
            Assert.False(ChartLabels.DrawTooltip(gui, new Rect(10, 10, 2, 2), new Vector2(11, 11), "Too small", style));
            Assert.True(ChartLabels.DrawTicks(gui, plot, new Rect(10, 85, 220, 30), ChartAxis.X,
                [0, .0001, 5, 10], style.Label, size: 11) is > 0 and < 4);
            Assert.Equal(2, ChartLegend.Draw(gui, new Rect(10, 10, 44, 100), [new("A", Color.Red), new("B", Color.Blue)],
                style.Label, size: 11));
        });
    }
}
