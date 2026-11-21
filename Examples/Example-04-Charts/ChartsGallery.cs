using System.Globalization;
using System.Numerics;
using Guinevere;
using Guinevere.Charts;

namespace Example04;

sealed partial class ChartsGallery
{
    static readonly ButtonGroupItem[] Views =
    [
        new("Line"), new("Bar"), new("Scatter"), new("Bubble"), new("Histogram"),
        new("Pie / donut"), new("Radar"), new("Timeline"), new("Flame"),
    ];
    static readonly ChartRange TrendTime = new(0, 100);
    static readonly ChartRange TrendValues = new(-1.3, 1.3);
    static readonly ChartRange Categories = new(-.5, 11.5);
    static readonly ChartRange CategoryValues = new(0, 12);
    static readonly ChartRange Samples = new(0, 1);
    static readonly ChartRange HistogramValues = new(0, 36);
    static readonly ChartRange FrameTime = new(0, 80);
    static readonly Color[] Palette =
    [
        Color.CornflowerBlue, Color.Orange, Color.MediumSeaGreen, Color.HotPink, Color.Gold,
    ];
    static readonly ChartLegendEntry[] CartesianLegend = [new("Observed", Palette[0]), new("Reference", Palette[1])];
    readonly Gui _gui;
    readonly ChartPoint[] _trend = new ChartPoint[100_000];
    readonly ChartPoint[] _reference = new ChartPoint[1_000];
    readonly int[] _decimated = new int[1_000];
    readonly ChartPoint[] _bars = new ChartPoint[12];
    readonly ChartPoint[] _scatter = new ChartPoint[80];
    readonly float[] _radii = new float[80];
    readonly ChartPoint[] _histogram = new ChartPoint[12];
    readonly int _decimatedCount;
    int _view;
    int _selected = -1;
    ChartRange _x;
    ChartRange _y;
    bool _donut = true;
    int _firstRow;
    int _nav;

    internal const int ViewCount = 9;

    internal int PlotCommands { get; private set; }

    internal ChartsGallery(Gui gui)
    {
        _gui = gui;
        for (var i = 0; i < _trend.Length; i++)
        {
            var x = TrendTime.Denormalize(i / (double)(_trend.Length - 1));
            _trend[i] = new ChartPoint(x, Math.Sin(x * .3) * .75 + Math.Sin(x * 7) * .17);
        }
        _decimatedCount = CartesianTransforms.Decimate(_trend, _decimated);
        for (var i = 0; i < _reference.Length; i++)
        {
            var x = TrendTime.Denormalize(i / (double)(_reference.Length - 1));
            _reference[i] = new ChartPoint(x, Math.Sin(x * .3) * .56 + .18);
        }
        for (var i = 0; i < _bars.Length; i++)
        {
            _bars[i] = new ChartPoint(i, 2 + i * 7 % 10);
        }
        for (var i = 0; i < _scatter.Length; i++)
        {
            var x = (i * 37 % 83) / 84d;
            var y = .15 + .65 * x + .12 * Math.Sin(i * 5);
            _scatter[i] = new ChartPoint(x, y);
            _radii[i] = 3 + i * 7 % 11;
        }
        Span<double> observations = stackalloc double[180];
        for (var i = 0; i < observations.Length; i++)
        {
            observations[i] = ((i * 43 % 181) + (i * 89 % 179)) / 360d;
        }
        Span<int> bins = stackalloc int[_histogram.Length];
        CartesianTransforms.Histogram(observations, Samples, bins);
        for (var i = 0; i < bins.Length; i++)
        {
            _histogram[i] = new ChartPoint((i + .5) / bins.Length, bins[i]);
        }
        PreparePolar();
        PrepareFlame();
        SelectView(0);
    }

    internal void SelectView(int view)
    {
        _view = view;
        (_x, _y) = view switch
        {
            0 => (TrendTime, TrendValues),
            1 => (Categories, CategoryValues),
            4 => (Samples, HistogramValues),
            7 or 8 => (FrameTime, Samples),
            _ => (Samples, Samples),
        };
        _selected = -1;
        _firstRow = 0;
    }

    internal void Draw()
    {
        using var root = _gui.StyledNode("charts-gallery", id: "gallery").Expand().Direction(Axis.Vertical).Enter();
        _gui.Label("Guinevere Charts");
        var toolbarView = _view;
        using (var toolbar = _gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Gap(8).Enter())
        {
            var view = _view;
            if (_gui.ButtonGroup(ref view, Views, id: "views").Changed)
            {
                SelectView(view);
                _gui.RequestFrame();
            }
        }
        DrawCommands(toolbarView);
        if (toolbarView == 8)
        {
            FlameNavigation();
        }
        using var chart = _gui.StyledNode("chart", id: "plot").Expand().Enter();
        var interaction = _gui.GetInteractable();
        if (_gui.Pass != Pass.Pass2Render)
        {
            return;
        }
        var area = _gui.CurrentNode.InnerRect;
        if (area.W <= 110 || area.H <= 100)
        {
            return;
        }
        var style = ChartStyle.Resolve(_gui);
        var plotArea = new Rect(area.X + 58, area.Y + 20, area.W - 82, area.H - 76);
        var shell = new CartesianPlot(plotArea, _x, _y);
        if (_view is < 5 or 7 or 8)
        {
            shell = Navigate(shell, interaction);
        }
        var before = _gui.CurrentNode.DrawList.Count;
        switch (_view)
        {
            case 5:
                DrawPie(plotArea, area, style, interaction);
                break;
            case 6:
                DrawRadar(plotArea, area, style, interaction);
                break;
            case 7:
            case 8:
                DrawTiming(shell, area, style, interaction);
                break;
            default:
                DrawCartesian(shell, area, style, interaction);
                break;
        }
        PlotCommands = _gui.CurrentNode.DrawList.Count - before;
    }

    void DrawCommands(int view)
    {
        using var toolbar = _gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Gap(6).Enter();
        if (view is 5)
        {
            if (_gui.Button(_donut ? "Show pie" : "Show donut", id: "hole"))
            {
                _donut = !_donut;
            }
            if (_gui.Button("Change values", id: "pie-values"))
            {
                StartPieAnimation();
            }
            _gui.Label("Click a slice to separate it.");
            return;
        }
        if (view is 6)
        {
            if (_gui.Button("Change values", id: "radar-values"))
            {
                StartRadarAnimation();
            }
            _gui.Label("Hover a vertex to inspect its value.");
            return;
        }
        if (_gui.Button("Zoom in", id: "zoom-in"))
        {
            _nav = 1;
        }
        if (_gui.Button("Zoom out", id: "zoom-out"))
        {
            _nav = -1;
        }
        if (_gui.Button("Reset", id: "reset"))
        {
            SelectView(_view);
        }
        if (view is 8)
        {
            if (_gui.Button("Up one lane", id: "lane-up"))
            {
                _firstRow = Math.Max(0, _firstRow - 1);
            }
            if (_gui.Button("Down one lane", id: "lane-down"))
            {
                _firstRow = Math.Min(_flameRows.Length - 1, _firstRow + 1);
            }
        }
        _gui.Label("Middle-drag to pan horizontally. Click an item to select it.");
    }

    CartesianPlot Navigate(CartesianPlot plot, InteractableElement interaction)
    {
        if (_nav != 0)
        {
            plot = plot.Zoom(plot.Area.Center, _nav > 0 ? 1.25 : .8, 1);
            _nav = 0;
        }
        if (interaction.OnDrag(out var drag, MouseButton.Middle))
        {
            plot = plot.Pan(new Vector2(drag.FrameDelta.X, 0));
        }
        _x = plot.X;
        _y = plot.Y;
        return plot;
    }

    void DrawCartesian(CartesianPlot plot, Rect bounds, ChartStyle style, InteractableElement interaction)
    {
        DrawAxes(plot, bounds, style);
        var pointer = _gui.Input.MousePosition;
        var hover = interaction.OnHover();
        var index = -1;
        var points = _view switch { 0 => _trend, 1 => _bars, 4 => _histogram, _ => _scatter };
        var width = Math.Max(1, plot.Area.W / points.Length * .7f);
        if (_view == 0)
        {
            var indices = _decimated.AsSpan(0, _decimatedCount);
            CartesianChart.DrawLine(_gui, plot, _reference, Palette[1], 2);
            CartesianChart.DrawLine(_gui, plot, points, Palette[0], 2, indices);
            index = hover ? CartesianChart.HitLineSample(plot, points, pointer, 9, indices) : -1;
            ChartLegend.Draw(_gui, new Rect(plot.Area.X, bounds.Y + bounds.H - 24, plot.Area.W, 24),
                CartesianLegend, style.Label);
        }
        else if (_view is 1 or 4)
        {
            CartesianChart.DrawBars(_gui, plot, points, Palette[0], width);
            index = hover ? CartesianChart.HitBars(plot, points, pointer, width) : -1;
        }
        else
        {
            ReadOnlySpan<float> radii = _view == 3 ? _radii : [];
            CartesianChart.DrawPoints(_gui, plot, points, Palette[0], radii: radii);
            index = hover ? CartesianChart.HitPoints(plot, points, pointer, radii: radii) : -1;
        }
        Pick(interaction, index);
        if ((uint)_selected < (uint)points.Length && plot.TryProject(points[_selected], out var position))
        {
            _gui.DrawCircleBorder(position, 7, style.Selection, 2);
        }
        if (index >= 0)
        {
            Span<char> text = stackalloc char[80];
            if (text.TryWrite(CultureInfo.InvariantCulture, $"x {points[index].X:F2} · y {points[index].Y:F2}", out var length))
            {
                ChartLabels.DrawTooltip(_gui, bounds, pointer, text[..length], style);
            }
        }
    }

    void DrawAxes(CartesianPlot plot, Rect bounds, ChartStyle style, bool vertical = true)
    {
        Span<double> x = stackalloc double[10];
        Span<double> y = stackalloc double[8];
        var xCount = CartesianTransforms.GenerateTicks(plot.X, x.Length, x);
        var yCount = vertical ? CartesianTransforms.GenerateTicks(plot.Y, y.Length, y) : 0;
        CartesianChart.DrawGrid(_gui, plot, x[..xCount], y[..yCount], style.Axis, style.Grid);
        ChartLabels.DrawTicks(_gui, plot, new Rect(bounds.X, plot.Area.Y + plot.Area.H + 4, bounds.W, 22),
            ChartAxis.X, x[..xCount], style.Label, size: 11);
        ChartLabels.DrawTicks(_gui, plot, new Rect(bounds.X, bounds.Y, plot.Area.X - bounds.X - 5, plot.Area.H + 40),
            ChartAxis.Y, y[..yCount], style.Label, size: 11);
    }

    bool Pick(InteractableElement interaction, int index)
    {
        if (!interaction.OnClick())
        {
            return false;
        }
        _selected = _selected == index ? -1 : index;
        _gui.RequestFrame();
        return true;
    }
}
