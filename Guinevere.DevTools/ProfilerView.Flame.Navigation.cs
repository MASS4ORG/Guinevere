using System.Numerics;
using Guinevere.Charts;

namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    const int VisibleFlameRows = 12;
    ChartRange _flameTime;
    int _firstFlameRow;
    int _flameZoom;
    int _flameSelected = -1;
    string? _flameBreadcrumb;
    string _flameQuery = "";
    string[] _flameNames = [];
    int[] _flameParents = [];
    int[] _flameMatches = [];
    int _flameMatchCount;
    int _nextFlameMatch;
    FrameRecord? _flameNamesFrame;

    void DrawFlameNavigation(FrameRecord frame)
    {
        var breadcrumb = _flameBreadcrumb;
        var parent = _flameSelected >= 0 ? _flameParents[_flameSelected] : -1;
        using (Gui.StyledNode("profiler-toolbar").ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            if (Gui.Button("Zoom in", id: "flame-zoom-in"))
            {
                _flameZoom = 1;
            }
            if (Gui.Button("Zoom out", id: "flame-zoom-out"))
            {
                _flameZoom = -1;
            }
            if (Gui.Button("Reset viewport", id: "flame-reset"))
            {
                ResetFlameViewport(frame);
            }
            if (Gui.Button("Rows up", enabled: _firstFlameRow > 0, id: "flame-rows-up"))
            {
                _firstFlameRow = Math.Max(0, _firstFlameRow - VisibleFlameRows);
            }
            if (Gui.Button("Rows down", enabled: _firstFlameRow < LastFlameRow, id: "flame-rows-down"))
            {
                _firstFlameRow = Math.Min(LastFlameRow, _firstFlameRow + VisibleFlameRows);
            }
            DrawFlameSearch(frame, parent);
        }
        if (breadcrumb is not null)
        {
            Gui.Label(breadcrumb, id: "flame-breadcrumb");
        }
    }

    void DrawFlameSearch(FrameRecord frame, int parent)
    {
        var canFind = _flameMatchCount > 0;
        if (Gui.TextInput(ref _flameQuery, placeholder: "Find a scope", id: "flame-search").Width(130).Changed)
        {
            UpdateFlameSearch();
            Gui.RequestFrame();
        }
        if (Gui.Button("Find next", enabled: canFind, id: "flame-find") && _flameMatchCount > 0)
        {
            RevealFlame(frame, _flameMatches[_nextFlameMatch]);
            _nextFlameMatch = (_nextFlameMatch + 1) % _flameMatchCount;
        }
        if (Gui.Button("Parent", enabled: parent >= 0, id: "flame-parent"))
        {
            RevealFlame(frame, parent);
        }
    }

    int LastFlameRow => Math.Max(0, _flameRows - VisibleFlameRows);

    void ResetFlameViewport(FrameRecord frame)
    {
        _firstFlameRow = 0;
        _flameZoom = 0;
        _flameTime = FrameTime(frame);
    }

    static ChartRange FrameTime(FrameRecord frame) => new(0, Math.Max(1, frame.End - frame.Start));

    ChartRange NavigateFlame(FrameRecord frame, Rect area, InteractableElement chart)
    {
        var plot = new CartesianPlot(area, _flameTime, new ChartRange(0, 1));
        if (_flameZoom != 0)
        {
            var anchor = area.Center;
            if (_flameSelected >= 0)
            {
                var scope = _flameIntervals[_flameSelected];
                var time = Math.Clamp((scope.Start + scope.End) / 2, _flameTime.Min, _flameTime.Max);
                if (plot.TryProject(new ChartPoint(time, .5), out var selected) && ContainsFlame(area, selected))
                {
                    anchor = selected;
                }
            }
            plot = plot.Zoom(anchor, _flameZoom > 0 ? 2 : .5, 1);
            _flameZoom = 0;
        }
        if (chart.OnDrag(out var drag, MouseButton.Middle))
        {
            plot = plot.Pan(new Vector2(drag.FrameDelta.X, 0));
        }
        return _flameTime = ConstrainFlameTime(plot.X, FrameTime(frame));
    }

    static ChartRange ConstrainFlameTime(ChartRange proposal, ChartRange frame)
    {
        var width = Math.Clamp(proposal.Max - proposal.Min, 1, frame.Max);
        var start = Math.Clamp(proposal.Min, 0, frame.Max - width);
        return new ChartRange(start, start + width);
    }

    void UpdateFlameSearch()
    {
        _nextFlameMatch = 0;
        _flameMatchCount = 0;
        if (_flameQuery.Length == 0)
        {
            return;
        }
        if (!ReferenceEquals(_flameNamesFrame, _flameFrame))
        {
            if (_flameNames.Length < _flame.Length)
            {
                _flameNames = new string[_flame.Length];
                _flameMatches = new int[_flame.Length];
            }
            for (var i = 0; i < _flame.Length; i++)
            {
                _flameNames[i] = _flame[i].Event.Name;
            }
            _flameNamesFrame = _flameFrame;
        }
        _flameMatchCount = FlameLayout.Search(_flameNames.AsSpan(0, _flame.Length), _flameQuery, _flameMatches);
    }

    void RevealFlame(FrameRecord frame, int index)
    {
        SelectFlame(frame, index);
        var scope = _flameIntervals[index];
        if (scope.Row < _firstFlameRow || scope.Row >= _firstFlameRow + VisibleFlameRows)
        {
            _firstFlameRow = Math.Clamp(scope.Row - 1, 0, LastFlameRow);
        }
        if (scope.Start < _flameTime.Min || scope.End > _flameTime.Max)
        {
            var width = Math.Max(scope.End - scope.Start, _flameTime.Max - _flameTime.Min);
            var center = (scope.Start + scope.End) / 2;
            _flameTime = ConstrainFlameTime(new ChartRange(center - width / 2, center + width / 2), FrameTime(frame));
        }
    }

    void SelectFlame(FrameRecord frame, int index)
    {
        _flameSelected = index;
        _selectedText = ScopeText(frame, _flame[index]);
        _flameBreadcrumb = MakeFlameBreadcrumb(index);
        Gui.RequestFrame();
    }

    string MakeFlameBreadcrumb(int index)
    {
        var name = _flame[index].Event.Name;
        var parent = _flameParents[index];
        if (parent < 0)
        {
            return name;
        }
        var parentName = _flame[parent].Event.Name;
        var ancestor = _flameParents[parent];
        if (ancestor < 0)
        {
            return $"{parentName} › {name}";
        }
        var prefix = _flameParents[ancestor] >= 0 ? "… › " : "";
        return $"{prefix}{_flame[ancestor].Event.Name} › {parentName} › {name}";
    }
}
