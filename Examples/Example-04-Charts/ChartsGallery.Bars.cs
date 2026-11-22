using System.Globalization;
using Guinevere;
using Guinevere.Charts;

namespace Example04;

sealed partial class ChartsGallery
{
    const int BarSeriesCount = 4;
    static readonly ButtonGroupItem[] BarModes = [new("Grouped"), new("Stacked"), new("Grouped stacks")];
    static readonly int[][] BarStacks = [[0, 1, 2, 3], [0, 0, 0, 0], [0, 0, 1, 1]];
    static readonly string[] BarNames = ["North", "South", "East", "West"];
    readonly double[] _barCategories = new double[12];
    readonly double[] _barValues = new double[12 * BarSeriesCount];
    readonly BarChartRect[] _barBounds = new BarChartRect[12 * BarSeriesCount];
    readonly ChartLegendEntry[] _barLegend = new ChartLegendEntry[BarSeriesCount];
    int _barMode;
    bool _horizontalBars;

    void PrepareBars()
    {
        for (var series = 0; series < BarSeriesCount; series++)
        {
            _barLegend[series] = new ChartLegendEntry(BarNames[series], Palette[series]);
        }
        for (var category = 0; category < _barCategories.Length; category++)
        {
            _barCategories[category] = category;
            for (var series = 0; series < BarSeriesCount; series++)
            {
                _barValues[category * BarSeriesCount + series] = (category * 7 + series * 3) % 11 - 5;
            }
        }
    }

    void DrawBarOptions()
    {
        _gui.ButtonGroup(ref _barMode, BarModes, id: "bar-layout");
        if (_gui.Button(_horizontalBars ? "Vertical bars" : "Horizontal bars", id: "bar-direction"))
        {
            _horizontalBars = !_horizontalBars;
            SelectView(1);
            _gui.RequestFrame();
        }
    }

    void DrawMultiBars(CartesianPlot plot, Rect bounds, ChartStyle style, InteractableElement interaction)
    {
        DrawAxes(plot, bounds, style);
        var categories = _horizontalBars ? plot.Y : plot.X;
        var extent = _horizontalBars ? plot.Area.H : plot.Area.W;
        var spacing = categories.Normalize(1) - categories.Normalize(0);
        var width = (float)Math.Clamp(spacing * extent * .76, 1, extent);
        var count = BarChart.Project(plot, _barCategories, _barValues, BarStacks[_barMode], _barBounds,
            width, horizontal: _horizontalBars);
        var bars = _barBounds.AsSpan(0, count);
        BarChart.Draw(_gui, bars, Palette);
        ChartLegend.Draw(_gui, new Rect(plot.Area.X, bounds.Y + bounds.H - 24, plot.Area.W, 24), _barLegend, style.Label);

        var pointer = _gui.Input.MousePosition;
        var hit = interaction.OnHover() ? BarChart.HitTest(bars, pointer) : -1;
        Pick(interaction, hit < 0 ? -1 : BarId(bars[hit]));
        foreach (var bar in bars)
        {
            if (BarId(bar) == _selected)
            {
                _gui.DrawRectBorder(bar.Bounds, style.Selection, thickness: 2);
                break;
            }
        }
        if (hit >= 0)
        {
            var bar = bars[hit];
            Span<char> text = stackalloc char[80];
            if (text.TryWrite(CultureInfo.InvariantCulture,
                $"{BarNames[bar.Series]} · category {bar.Category + 1} · {_barValues[BarId(bar)]:+0;-0;0}", out var length))
            {
                ChartLabels.DrawTooltip(_gui, bounds, pointer, text[..length], style);
            }
        }
    }

    static int BarId(BarChartRect bar) => bar.Category * BarSeriesCount + bar.Series;
}
