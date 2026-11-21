using System.Globalization;
using Guinevere;
using Guinevere.Charts;

namespace Example04;

sealed partial class ChartsGallery
{
    const int IntervalsPerLane = 512;
    readonly TimelineSpan[] _laneIntervals =
    [
        new(0, 18, 0, Palette[0]), new(22, 54, 0, Palette[2]),
        new(12, 38, 1, Palette[1]), new(42, 68, 1, Palette[3]),
        new(5, 28, 2, Palette[4]), new(34, 76, 2, Palette[0]),
    ];
    static readonly string[] IntervalNames = ["Compile", "Link", "Read", "Write", "Validate", "Publish"];
    static readonly string[] FrameScopes = ["Frame", "Dispatch", "Prepare", "Simulate", "Draw", "Present"];
    const int FlameLaneCount = 48;
    readonly ReadOnlyMemory<FlameInterval>[] _flameRows = new ReadOnlyMemory<FlameInterval>[FlameLaneCount];
    readonly int[] _flameParents = new int[FlameLaneCount * IntervalsPerLane];
    readonly string[] _flameNames = new string[FlameLaneCount * IntervalsPerLane];
    readonly int[] _flameMatches = new int[FlameLaneCount * IntervalsPerLane];
    string _search = "";
    int _matchCount;
    int _matchIndex;

    void PrepareFlame()
    {
        for (var row = 0; row < _flameRows.Length; row++)
        {
            var values = new FlameInterval[IntervalsPerLane];
            for (var i = 0; i < values.Length; i++)
            {
                var index = row * IntervalsPerLane + i;
                var start = i * 20 + row * .12;
                values[i] = new FlameInterval(start, start + 18 + i % 2 - row * .3, index);
                _flameNames[index] = FrameScopes[row == 0 ? 0 : 1 + (row + i) % (FrameScopes.Length - 1)];
                _flameParents[index] = row == 0 ? -1 : index - IntervalsPerLane;
            }
            _flameRows[row] = values;
        }
    }

    void FlameNavigation()
    {
        var selected = _selected;
        var canFind = _matchCount > 0;
        using var row = _gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Gap(6).Enter();
        if (_gui.TextInput(ref _search, placeholder: "Find a scope", id: "find-scope").Width(160).Changed)
        {
            _matchCount = FlameLayout.Search(_flameNames, _search, _flameMatches);
            _matchIndex = 0;
            _gui.RequestFrame();
        }
        if (_gui.Button("Find next", enabled: canFind, id: "find-next") && _matchCount > 0)
        {
            RevealFlame(_flameMatches[_matchIndex]);
            _matchIndex = (_matchIndex + 1) % _matchCount;
        }
        var parent = selected >= 0 ? _flameParents[selected] : -1;
        if (_gui.Button("Select parent", enabled: parent >= 0, id: "select-parent"))
        {
            RevealFlame(parent);
        }
        Span<int> path = stackalloc int[FlameLaneCount];
        var length = FlameLayout.WritePath(_flameParents, selected, path);
        if (length > 0)
        {
            Span<char> caption = stackalloc char[100];
            var position = 0;
            for (var i = Math.Max(0, length - 3); i < length; i++)
            {
                if (position > 0)
                {
                    " › ".CopyTo(caption[position..]);
                    position += 3;
                }
                var name = _flameNames[path[i]].AsSpan();
                name.CopyTo(caption[position..]);
                position += name.Length;
            }
            _gui.DrawText(caption[..position], size: 12);
        }
    }

    void RevealFlame(int index)
    {
        _selected = index;
        _gui.RequestFrame();
        var lane = index / IntervalsPerLane;
        _firstRow = Math.Max(0, lane - 2);
        var interval = _flameRows[lane].Span[index % IntervalsPerLane];
        if (interval.Start >= _x.Min && interval.End <= _x.Max)
        {
            return;
        }
        var center = (interval.Start + interval.End) / 2;
        var radius = Math.Max(interval.End - interval.Start, _x.Max - _x.Min) / 2;
        _x = new ChartRange(center - radius, center + radius);
    }

    void DrawTiming(CartesianPlot shell, Rect bounds, ChartStyle style, InteractableElement interaction)
    {
        DrawAxes(shell, bounds, style, vertical: false);
        var flame = _view == 8;
        var plot = new TimelinePlot(shell.Area, shell.X, flame ? 26 : 52);
        var pointer = _gui.Input.MousePosition;
        var hover = interaction.OnHover();
        if (flame)
        {
            var index = hover ? FlameChart.HitTest(plot, _flameRows, pointer, _firstRow) : -1;
            Pick(interaction, index);
            FlameChart.Draw(_gui, plot, _flameRows, Palette, _firstRow, _selected, style.Selection);
            if (index >= 0)
            {
                var lane = index / IntervalsPerLane;
                var span = _flameRows[lane].Span[index % IntervalsPerLane];
                Span<char> text = stackalloc char[96];
                if (text.TryWrite(CultureInfo.InvariantCulture,
                    $"{_flameNames[index]} · lane {lane + 1} · {span.Start:F1}–{span.End:F1} ms", out var length))
                {
                    ChartLabels.DrawTooltip(_gui, bounds, pointer, text[..length], style);
                }
            }
            return;
        }

        var hit = hover ? TimelineChart.HitTest(plot, _laneIntervals, pointer, rowGap: 8) : -1;
        Pick(interaction, hit);
        TimelineChart.Draw(_gui, plot, _laneIntervals, rowGap: 8);
        for (var i = 0; i < _laneIntervals.Length; i++)
        {
            var interval = _laneIntervals[i];
            if (!plot.TryProject(interval.Start, interval.End, interval.Row, out var item, rowGap: 8))
            {
                continue;
            }
            ChartLabels.Draw(_gui, new Rect(item.X + 5, item.Y + 4, item.W - 10, item.H - 8),
                IntervalNames[i], Color.Black);
            if (_selected == i)
            {
                _gui.DrawRectBorder(item, style.Selection, 2);
            }
        }
        if (hit >= 0)
        {
            ChartLabels.DrawTooltip(_gui, bounds, pointer, IntervalNames[hit], style);
        }
    }
}
