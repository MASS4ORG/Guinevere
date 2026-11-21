using System.Globalization;
using System.Numerics;
using Guinevere;
using Guinevere.Charts;

namespace Example04;

sealed partial class ChartsGallery
{
    static readonly string[] Regions = ["North", "South", "East", "West", "Online"];
    static readonly double[] Weights = [30, 24, 18, 16, 12];
    static readonly double[] AlternateWeights = [19, 31, 22, 20, 8];
    static readonly string[] Dimensions = ["Speed", "Quality", "Coverage", "Stability", "Usability"];
    static readonly double[] Observed = [82, 6.6, .91, 54, 7.3];
    static readonly double[] AlternateObserved = [63, 8.2, .79, 76, 9.1];
    static readonly double[] Reference = [70, 8.3, .8, 68, 6.5];
    static readonly ChartRange[] RadarRanges = [new(0, 100), new(0, 10), new(0, 1), new(0, 100), new(0, 10)];
    static readonly ButtonGroupItem[] PieModes = [new("Pie"), new("Donut"), new("Arc"), new("Two rings")];
    readonly PieSegment[] _slices = new PieSegment[Weights.Length];
    readonly PieSegment[] _innerSlices = new PieSegment[Weights.Length];
    readonly ChartLegendEntry[] _pieLegend = new ChartLegendEntry[Weights.Length];
    readonly Vector2[] _radarAxes = new Vector2[Dimensions.Length];
    readonly Vector2[] _observedRadar = new Vector2[Dimensions.Length];
    readonly Vector2[] _referenceRadar = new Vector2[Dimensions.Length];
    readonly double[] _pieFrom = new double[Weights.Length];
    readonly double[] _pieValues = (double[])Weights.Clone();
    readonly double[] _radarFrom = new double[Observed.Length];
    readonly double[] _radarValues = (double[])Observed.Clone();
    double _pieStarted = double.NegativeInfinity;
    double _radarStarted = double.NegativeInfinity;
    bool _pieAlternate;
    bool _radarAlternate;
    bool _radarReferenceSelected;
    int _pieMode = 1;
    bool _emptyPie;

    void PreparePolar()
    {
        PieChart.CreateSegments(AlternateWeights, _innerSlices, -Math.PI / 2, angularGap: .025);
        for (var i = 0; i < Weights.Length; i++)
        {
            _pieLegend[i] = new ChartLegendEntry(Regions[i], Palette[i]);
        }
        RadarProjection.CreateAxes(_radarAxes);
        RadarProjection.Project(RadarRanges, Observed, _observedRadar);
        RadarProjection.Project(RadarRanges, Reference, _referenceRadar);
    }

    void DrawPieOptions()
    {
        if (_gui.ButtonGroup(ref _pieMode, PieModes, id: "pie-shape").Changed)
        {
            _selected = -1;
        }
        if (_gui.Button("Change values", enabled: !_emptyPie, id: "pie-values"))
        {
            StartPieAnimation();
        }
        if (_gui.Button(_emptyPie ? "Show data" : "Show empty state", id: "pie-empty"))
        {
            _emptyPie = !_emptyPie;
            _selected = -1;
        }
    }

    void DrawPie(Rect plot, Rect bounds, ChartStyle style, InteractableElement interaction)
    {
        var size = Math.Min(plot.W, plot.H) - 24;
        if (size <= 0)
        {
            return;
        }
        var area = new Rect(plot.Center.X - size / 2, plot.Center.Y - size / 2, size, size);
        Animate(_pieFrom, _pieAlternate ? AlternateWeights : Weights, _pieValues, ref _pieStarted);
        var count = _emptyPie ? 0 : PieChart.CreateSegments(_pieValues, _slices, -Math.PI / 2, angularGap: .025,
            angularSweep: _pieMode == 2 ? Math.PI * 1.5 : Math.Tau);
        if (count == 0)
        {
            ChartLabels.Draw(_gui, area, "No positive values", style.Label, 14,
                horizontal: ChartLabelAlignment.Center, vertical: ChartLabelAlignment.Center);
            return;
        }
        var slices = _slices.AsSpan(0, count);
        var hole = _pieMode switch { 0 => 0, 3 => .64f, _ => .48f };
        var pointer = _gui.Input.MousePosition;
        var hover = interaction.OnHover();
        var source = hover ? PieChart.HitTest(area, slices, pointer, hole, OuterPieSelection(style)) : -1;
        var inner = source < 0 && hover && _pieMode == 3;
        if (inner)
        {
            source = PieChart.HitTest(area, _innerSlices, pointer, .25, InnerPieSelection(style), outerRadius: .59);
        }
        Pick(interaction, source < 0 ? -1 : source + (inner ? Weights.Length : 0));
        if (_pieMode == 3)
        {
            PieChart.Draw(_gui, area, _innerSlices, Palette, .25f, InnerPieSelection(style), outerRadius: .59f);
        }
        PieChart.Draw(_gui, area, slices, Palette, hole, OuterPieSelection(style));
        PieChart.DrawLabels(_gui, area, slices, Regions, style.Label, hole, OuterPieSelection(style), size: 12);
        ChartLegend.Draw(_gui, new Rect(plot.X, bounds.Y + bounds.H - 42, plot.W, 42), _pieLegend, style.Label);
        if (source >= 0)
        {
            Span<char> text = stackalloc char[64];
            var value = inner ? AlternateWeights[source] : _pieValues[source];
            if (text.TryWrite(CultureInfo.InvariantCulture, $"{Regions[source]} · {value:F0}%", out var length))
            {
                ChartLabels.DrawTooltip(_gui, bounds, pointer, text[..length], style);
            }
        }
    }

    PieSelection OuterPieSelection(ChartStyle style) => new(_selected, 10, style.Selection);

    PieSelection InnerPieSelection(ChartStyle style) => new(_selected - Weights.Length, 0, style.Selection);

    void DrawRadar(Rect plot, Rect bounds, ChartStyle style, InteractableElement interaction)
    {
        if (Animate(_radarFrom, _radarAlternate ? AlternateObserved : Observed, _radarValues, ref _radarStarted))
        {
            RadarProjection.Project(RadarRanges, _radarValues, _observedRadar);
        }
        var size = Math.Min(plot.W, plot.H) - 68;
        if (size <= 0)
        {
            return;
        }
        var area = new Rect(plot.Center.X - size / 2, plot.Center.Y - size / 2, size, size);
        RadarChart.DrawGrid(_gui, area, _radarAxes, 5, style.Grid);
        RadarChart.DrawSeries(_gui, area, _referenceRadar, Palette[1], 2);
        RadarChart.DrawSeries(_gui, area, _observedRadar, Palette[0], 3);
        Span<Rect> labels = stackalloc Rect[Dimensions.Length];
        RadarChart.DrawAxisLabels(_gui, area, plot, _radarAxes, Dimensions, labels, style.Label, size: 12);
        ChartLegend.Draw(_gui, new Rect(plot.X, bounds.Y + bounds.H - 24, plot.W, 24), CartesianLegend, style.Label);
        var pointer = _gui.Input.MousePosition;
        var hovered = interaction.OnHover();
        var index = hovered ? RadarChart.HitSeries(area, _observedRadar, pointer, 8) : -1;
        var values = _radarValues;
        var reference = hovered && index < 0;
        if (reference)
        {
            index = RadarChart.HitSeries(area, _referenceRadar, pointer, 8);
            values = Reference;
        }
        if (Pick(interaction, index))
        {
            _radarReferenceSelected = reference;
        }
        if ((uint)_selected < (uint)_observedRadar.Length)
        {
            var vertices = _radarReferenceSelected ? _referenceRadar : _observedRadar;
            _gui.DrawCircleBorder(area.Center + vertices[_selected] * size / 2, 7, style.Selection, 2);
        }
        if (index >= 0)
        {
            var value = values[index];
            Span<char> text = stackalloc char[80];
            if (text.TryWrite(CultureInfo.InvariantCulture, $"{Dimensions[index]} · {value:G4}", out var length))
            {
                ChartLabels.DrawTooltip(_gui, bounds, pointer, text[..length], style);
            }
        }
        else if (hovered)
        {
            var dimension = RadarProjection.HitTestAxis((pointer - area.Center) / (size / 2),
                Dimensions.Length, 16 / (size / 2));
            if (dimension >= 0)
            {
                ChartLabels.DrawTooltip(_gui, bounds, pointer, Dimensions[dimension], style);
            }
        }
    }

    void StartPieAnimation()
    {
        _pieValues.CopyTo(_pieFrom, 0);
        _pieAlternate = !_pieAlternate;
        _pieStarted = _gui.Time.Elapsed;
    }

    void StartRadarAnimation()
    {
        _radarValues.CopyTo(_radarFrom, 0);
        _radarAlternate = !_radarAlternate;
        _radarStarted = _gui.Time.Elapsed;
    }

    bool Animate(double[] from, double[] to, double[] output, ref double started)
    {
        if (!double.IsFinite(started))
        {
            return false;
        }
        var fraction = Math.Clamp((_gui.Time.Elapsed - started) / .35, 0, 1);
        ChartAnimation.Interpolate(from, to, fraction, output);
        if (fraction < 1)
        {
            _gui.RequestFrame();
        }
        else
        {
            started = double.NegativeInfinity;
        }
        return true;
    }
}
