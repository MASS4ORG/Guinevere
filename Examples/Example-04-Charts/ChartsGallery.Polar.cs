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
    readonly PieSegment[] _slices = new PieSegment[Weights.Length];
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

    void PreparePolar()
    {
        PieChart.CreateSegments(Weights, _slices, -Math.PI / 2, angularGap: .025);
        for (var i = 0; i < Weights.Length; i++)
        {
            _pieLegend[i] = new ChartLegendEntry(Regions[i], Palette[i]);
        }
        RadarProjection.CreateAxes(_radarAxes);
        RadarProjection.Project(RadarRanges, Observed, _observedRadar);
        RadarProjection.Project(RadarRanges, Reference, _referenceRadar);
    }

    void DrawPie(Rect plot, Rect bounds, ChartStyle style, InteractableElement interaction)
    {
        if (Animate(_pieFrom, _pieAlternate ? AlternateWeights : Weights, _pieValues, ref _pieStarted))
        {
            PieChart.CreateSegments(_pieValues, _slices, -Math.PI / 2, angularGap: .025);
        }
        var size = Math.Min(plot.W, plot.H) - 24;
        if (size <= 0)
        {
            return;
        }
        var area = new Rect(plot.Center.X - size / 2, plot.Center.Y - size / 2, size, size);
        var hole = _donut ? .48f : 0;
        var pointer = _gui.Input.MousePosition;
        var selection = new PieSelection(_selected, 10, style.Selection);
        var index = interaction.OnHover() ? PieChart.HitTest(area, _slices, pointer, hole, selection) : -1;
        Pick(interaction, index);
        PieChart.Draw(_gui, area, _slices, Palette, hole, selection with { SourceIndex = _selected });
        ChartLegend.Draw(_gui, new Rect(plot.X, bounds.Y + bounds.H - 42, plot.W, 42), _pieLegend, style.Label);
        if (index >= 0)
        {
            Span<char> text = stackalloc char[64];
            if (text.TryWrite(CultureInfo.InvariantCulture, $"{Regions[index]} · {_pieValues[index]:F0}%", out var length))
            {
                ChartLabels.DrawTooltip(_gui, bounds, pointer, text[..length], style);
            }
        }
    }

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
        for (var i = 0; i < _radarAxes.Length; i++)
        {
            var point = area.Center + _radarAxes[i] * (size / 2 + 16);
            var label = _gui.LayoutPlainText(Dimensions[i], size: 12);
            _gui.DrawTextLayout(label, point - new Vector2(label.Width, label.Height) / 2, style.Label);
        }
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
