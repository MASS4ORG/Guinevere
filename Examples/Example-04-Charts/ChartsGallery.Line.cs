using System.Numerics;
using Guinevere;
using Guinevere.Charts;

namespace Example04;

sealed partial class ChartsGallery
{
    readonly ChartPoint[] _referenceOriginal = new ChartPoint[1_000];
    readonly ChartPoint[] _referenceAlternative = new ChartPoint[1_000];
    readonly ChartPoint[] _referenceFrom = new ChartPoint[1_000];
    double _referenceStarted = double.NegativeInfinity;
    bool _alternateReference;

    void DrawLineOptions()
    {
        if (_gui.Button(_smoothLine ? "Straight reference" : "Smooth reference", id: "line-mode"))
        {
            _smoothLine = !_smoothLine;
        }
        if (_gui.Button(_farScales ? "Bottom and left scales" : "Top and right scales", id: "scale-sides"))
        {
            _farScales = !_farScales;
        }
        if (_gui.Button("Animate reference", id: "reference-motion"))
        {
            _reference.CopyTo(_referenceFrom, 0);
            _alternateReference = !_alternateReference;
            _referenceStarted = _gui.Time.Elapsed;
        }
    }

    void DrawTrend(CartesianPlot plot, Rect bounds, ChartStyle style, InteractableElement interaction,
        Vector2 pointer, bool hover)
    {
        ChartLabels.Draw(_gui, new Rect(bounds.X, bounds.Y, bounds.W, 20), "Observed and reference",
            style.Label, 14, horizontal: _farScales ? ChartLabelAlignment.End : ChartLabelAlignment.Center);
        if (double.IsFinite(_referenceStarted))
        {
            var progress = Math.Clamp((_gui.Time.Elapsed - _referenceStarted) / .35, 0, 1);
            ChartAnimation.InterpolatePoints(_referenceFrom,
                _alternateReference ? _referenceAlternative : _referenceOriginal, progress, _reference);
            if (progress < 1)
            {
                _gui.RequestFrame();
            }
            else
            {
                _referenceStarted = double.NegativeInfinity;
            }
        }
        if (_smoothLine)
        {
            CartesianChart.DrawSmoothLine(_gui, plot, _reference, Palette[1], 2);
        }
        else
        {
            CartesianChart.DrawLine(_gui, plot, _reference, Palette[1], 2);
        }
        if (_decimatedRange != plot.X)
        {
            _decimatedCount = CartesianTransforms.DecimateVisible(_trend, plot.X, _decimated);
            _decimatedRange = plot.X;
        }
        ChartLegend.Draw(_gui, new Rect(plot.Area.X, bounds.Y + bounds.H - 24, plot.Area.W, 24),
            CartesianLegend, style.Label);
        var observed = -1;
        if (_decimatedCount != 0)
        {
            var indices = _decimated.AsSpan(0, _decimatedCount);
            CartesianChart.DrawLine(_gui, plot, _trend, Palette[0], 2, indices);
            if (hover)
            {
                observed = CartesianChart.HitLineSample(plot, _trend, pointer, 9, indices);
            }
        }
        var reference = hover && observed < 0 ? CartesianChart.HitLineSample(plot, _reference, pointer, 9) : -1;
        Pick(interaction, reference >= 0 ? _trend.Length + reference : observed);
        DrawSampleSelection(plot, style, _trend, _selected);
        DrawSampleSelection(plot, style, _reference, _selected - _trend.Length);
        if (observed >= 0)
        {
            DrawSampleTooltip(bounds, style, pointer, _trend[observed], "Observed · ");
        }
        else if (reference >= 0)
        {
            DrawSampleTooltip(bounds, style, pointer, _reference[reference], "Reference · ");
        }
    }
}
