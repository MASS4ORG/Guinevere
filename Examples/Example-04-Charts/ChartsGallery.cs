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
    static readonly ChartRange CategoryValues = new(-12, 12);
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
    readonly ChartPoint[] _scatter = new ChartPoint[80];
    readonly float[] _radii = new float[80];
    readonly ChartPoint[] _histogram = new ChartPoint[12];
    int _decimatedCount;
    ChartRange _decimatedRange;
    bool _smoothLine;
    bool _farScales;
    int _view;
    int _selected = -1;
    ChartRange _x;
    ChartRange _y;
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
        for (var i = 0; i < _reference.Length; i++)
        {
            var x = TrendTime.Denormalize(i / (double)(_reference.Length - 1));
            _reference[i] = new ChartPoint(x, Math.Sin(x * .3) * .56 + .18);
            _referenceOriginal[i] = _reference[i];
            _referenceAlternative[i] = new ChartPoint(x, Math.Cos(x * .25) * .68 - .18);
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
        PrepareBars();
        SelectView(0);
    }

    internal void SelectView(int view)
    {
        _view = view;
        (_x, _y) = view switch
        {
            0 => (TrendTime, TrendValues),
            1 => _horizontalBars ? (CategoryValues, Categories) : (Categories, CategoryValues),
            4 => (Samples, HistogramValues),
            7 or 8 => (FrameTime, Samples),
            _ => (Samples, Samples),
        };
        _selected = -1;
        _firstRow = 0;
    }

    internal void CheckVariants(Action<string> render)
    {
        SelectView(0);
        _smoothLine = _farScales = true;
        render("smooth line with top and right scales");
        for (var orientation = 0; orientation < 2; orientation++)
        {
            _horizontalBars = orientation == 1;
            SelectView(1);
            for (_barMode = 0; _barMode < BarModes.Length; _barMode++)
            {
                render($"bar mode {_barMode}, horizontal {_horizontalBars}");
            }
        }
        SelectView(5);
        for (_pieMode = 0; _pieMode < PieModes.Length; _pieMode++)
        {
            render($"pie mode {_pieMode}");
        }
        _emptyPie = true;
        render("empty pie");
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
        var farScales = _view == 0 && _farScales;
        var top = _view == 0 ? 44 : 20;
        var plotArea = new Rect(area.X + (farScales ? 24 : 58), area.Y + top, area.W - 82, area.H - 56 - top);
        var shell = new CartesianPlot(plotArea, _x, _y);
        if (_view is < 5 or 7 or 8)
        {
            shell = Navigate(shell, interaction);
        }
        var before = _gui.CurrentNode.DrawList.Count;
        switch (_view)
        {
            case 1:
                DrawMultiBars(shell, area, style, interaction);
                break;
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
        if (view == 0)
        {
            DrawLineOptions();
        }
        if (view == 1)
        {
            DrawBarOptions();
        }
        if (view is 5)
        {
            DrawPieOptions();
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
        if (_view == 0)
        {
            DrawTrend(plot, bounds, style, interaction, pointer, hover);
            return;
        }
        var points = _view == 4 ? _histogram : _scatter;
        int index;
        if (_view == 4)
        {
            var width = Math.Max(1, plot.Area.W / points.Length * .7f);
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
        DrawSampleSelection(plot, style, points, _selected);
        if (index >= 0)
        {
            DrawSampleTooltip(bounds, style, pointer, points[index]);
        }
    }

    void DrawSampleSelection(CartesianPlot plot, ChartStyle style, ReadOnlySpan<ChartPoint> points, int selection)
    {
        if ((uint)selection < (uint)points.Length && plot.TryProject(points[selection], out var position))
        {
            _gui.DrawCircleBorder(position, 7, style.Selection, 2);
        }
    }

    void DrawSampleTooltip(Rect bounds, ChartStyle style, Vector2 pointer, ChartPoint point,
        ReadOnlySpan<char> prefix = default)
    {
        Span<char> text = stackalloc char[80];
        if (text.TryWrite(CultureInfo.InvariantCulture, $"{prefix}x {point.X:F2} · y {point.Y:F2}", out var length))
        {
            ChartLabels.DrawTooltip(_gui, bounds, pointer, text[..length], style);
        }
    }

    void DrawAxes(CartesianPlot plot, Rect bounds, ChartStyle style, bool vertical = true)
    {
        Span<double> x = stackalloc double[10];
        Span<double> y = stackalloc double[8];
        var xCount = CartesianTransforms.GenerateTicks(plot.X, x.Length, x);
        var yCount = vertical ? CartesianTransforms.GenerateTicks(plot.Y, y.Length, y) : 0;
        CartesianChart.DrawGrid(_gui, plot, x[..xCount], y[..yCount], style.Axis, style.Grid);
        var far = _view == 0 && _farScales;
        var xSide = far ? ChartAxisSide.Top : ChartAxisSide.Bottom;
        var ySide = far ? ChartAxisSide.Right : ChartAxisSide.Left;
        CartesianChart.DrawScale(_gui, plot, xSide, x[..xCount], style.Axis);
        ChartLabels.DrawTicks(_gui, plot,
            new Rect(bounds.X, far ? plot.Area.Y - 24 : plot.Area.Y + plot.Area.H + 5, bounds.W, 19),
            xSide, x[..xCount], style.Label, size: 11);
        if (vertical)
        {
            CartesianChart.DrawScale(_gui, plot, ySide, y[..yCount], style.Axis);
            var labelX = far ? plot.Area.X + plot.Area.W + 5 : bounds.X;
            ChartLabels.DrawTicks(_gui, plot, new Rect(labelX, plot.Area.Y - 10, 53, plot.Area.H + 20),
                ySide, y[..yCount], style.Label, size: 11);
        }
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
