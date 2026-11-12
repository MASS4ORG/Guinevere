using System.Globalization;
using System.Numerics;
using MASS4.Profiling;

namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    const float StatRowHeight = 18;
    const float StatColumnWidth = 82;
    static readonly string[] StatColumns = ["min ms", "median ms", "max ms", "calls"];
    (Guid Source, long Generation, int Count) _statsKey;
    ScopeStat[] _stats = [];
    int _hoveredStat = -1;

    /// <summary>One scope path (track and ancestor names) summarized over the recorded frames.</summary>
    /// <remarks>Per-frame values sum every call on the path, so a scope entered twice counts once per frame.</remarks>
    sealed record ScopeStat(string Name, int Track, int Depth, double Min, double Median, double Max,
        double CallsPerFrame, int Frames, FrameRecord Worst);

    /// <summary>Mutable accumulator for one scope path while scanning the recorded frames.</summary>
    sealed class StatPath(string name, int track, int depth)
    {
        public string Name { get; } = name;
        public int Track { get; } = track;
        public int Depth { get; } = depth;
        public List<string> Children { get; } = [];
        public List<double> Samples { get; } = [];
        public long Calls { get; set; }
        public FrameRecord? Worst { get; private set; }
        double _worst = -1;

        public void Add(FrameRecord frame, double milliseconds)
        {
            Samples.Add(milliseconds);
            if (milliseconds > _worst)
            {
                _worst = milliseconds;
                Worst = frame;
            }
        }

        public ScopeStat ToStat()
        {
            var sorted = Samples.Order().ToArray();
            return new ScopeStat(Name, Track, Depth, sorted[0], sorted[sorted.Length / 2], sorted[^1],
                (double)Calls / sorted.Length, sorted.Length, Worst!);
        }
    }

    /// <summary>Min/median/max per scope path over the recorded frames, nested like the flame chart.</summary>
    void ScopeTable()
    {
        PrepareStats();
        if (_stats.Length == 0)
        {
            return;
        }
        Gui.Label(string.Create(CultureInfo.InvariantCulture,
            $"Scopes over {_frames.Count} frames; click a row to select its slowest frame."));
        var height = (_stats.Length + 1) * StatRowHeight + 4;
        using (Gui.StyledNode("profiler-scopes", id: "profiler-scopes").ExpandWidth().Height(height).Enter())
        {
            var table = Gui.GetInteractable();
            if (Gui.Pass != Pass.Pass2Render)
            {
                return;
            }
            var area = Gui.CurrentNode.Rect;
            var hovered = TrackStatPointer(area, table);
            DrawStatRow(area, 0, "scope", StatColumns, StyleColor("profiler-budget", "border-color"), 0);
            for (var index = 0; index < _stats.Length; index++)
            {
                DrawStat(area, index, index == hovered);
            }
        }
    }

    /// <summary>Updates the hovered row; a click selects the row's slowest frame and its text for copying.</summary>
    int TrackStatPointer(Rect area, InteractableElement table)
    {
        var hovered = -1;
        if (table.OnHover())
        {
            var row = (int)Math.Floor((Gui.Input.MousePosition.Y - area.Y - 2) / StatRowHeight) - 1;
            hovered = row >= 0 && row < _stats.Length ? row : -1;
        }
        if (hovered >= 0 && table.OnClick())
        {
            _selectedText = StatText(_stats[hovered]);
            Command("frame", _stats[hovered].Worst.Generation.ToString(CultureInfo.InvariantCulture));
        }
        if (hovered != _hoveredStat)
        {
            _hoveredStat = hovered;
            Gui.RequestFrame();
        }
        return hovered;
    }

    void DrawStat(Rect area, int index, bool hovered)
    {
        var stat = _stats[index];
        var top = area.Y + 2 + (index + 1) * StatRowHeight;
        if (hovered)
        {
            Gui.DrawRect(new Rect(area.X, top, area.W, StatRowHeight), StyleColor("profiler-hover", "background-color"));
        }
        Gui.DrawRect(new Rect(area.X + 6 + stat.Depth * 12, top + 4, 10, 10), SeriesColor(stat.Name), 2);
        string[] values =
        [
            Format(stat.Min), Format(stat.Median), Format(stat.Max),
            string.Create(CultureInfo.InvariantCulture, $"{stat.CallsPerFrame:0.#}")
        ];
        DrawStatRow(area, index + 1, stat.Name, values, StyleColor("profiler", "color"), 20 + stat.Depth * 12);
    }

    static string Format(double milliseconds) => milliseconds.ToString("F3", CultureInfo.InvariantCulture);

    /// <summary>Draws a name column and right-aligned value columns; the name is dropped when columns overlap it.</summary>
    void DrawStatRow(Rect area, int row, string name, string[] values, Color color, float indent)
    {
        var top = area.Y + 2 + row * StatRowHeight;
        var columns = area.X + area.W - values.Length * StatColumnWidth;
        var label = Gui.LayoutText(name, size: 11);
        if (area.X + 6 + indent + label.Width < columns)
        {
            Gui.DrawTextLayout(label, new Vector2(area.X + 6 + indent, top + (StatRowHeight - label.Height) / 2), color);
        }
        for (var column = 0; column < values.Length; column++)
        {
            var value = Gui.LayoutText(values[column], size: 11);
            var right = columns + (column + 1) * StatColumnWidth - 6;
            Gui.DrawTextLayout(value, new Vector2(right - value.Width, top + (StatRowHeight - value.Height) / 2), color);
        }
    }

    static string StatText(ScopeStat stat) => string.Create(CultureInfo.InvariantCulture,
        $"{stat.Name} · track {stat.Track} · min {stat.Min:F3} ms · median {stat.Median:F3} ms · max {stat.Max:F3} ms (frame #{stat.Worst.Frame}) · {stat.CallsPerFrame:0.##} calls/frame · in {stat.Frames} frames");

    /// <summary>Rebuilds the table when the recorded window changes; rows follow track, then nesting, then first use.</summary>
    void PrepareStats()
    {
        var key = (_recordedSource, _frames.Count > 0 ? _frames[^1].Generation : 0, _frames.Count);
        if (key == _statsKey)
        {
            return;
        }
        _statsKey = key;
        var paths = new Dictionary<string, StatPath>(StringComparer.Ordinal);
        var roots = new List<string>();
        foreach (var frame in _frames)
        {
            CollectStats(frame, paths, roots);
        }
        _stats = [.. Flatten(roots.OrderBy(root => paths[root].Track), paths).Select(static path => path.ToStat())];
        _hoveredStat = -1;
    }

    static IEnumerable<StatPath> Flatten(IEnumerable<string> keys, Dictionary<string, StatPath> paths) =>
        keys.SelectMany(key => Flatten(paths[key].Children, paths).Prepend(paths[key]));

    static void CollectStats(FrameRecord frame, Dictionary<string, StatPath> paths, List<string> roots)
    {
        var byId = frame.Scopes.ToDictionary(static entry => entry.Id);
        var keys = new Dictionary<long, string>();
        var totals = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var scope in frame.Scopes)
        {
            var key = PathKey(scope, byId, keys, paths, roots);
            totals[key] = totals.GetValueOrDefault(key) + scope.End - scope.Start;
            paths[key].Calls++;
        }
        foreach (var (key, ticks) in totals)
        {
            paths[key].Add(frame, frame.ToMilliseconds(ticks));
        }
    }

    /// <summary>Path of ancestor names on the same track; malformed parent cycles end at the first repeat.</summary>
    static string PathKey(ProfileEvent scope, Dictionary<long, ProfileEvent> byId, Dictionary<long, string> keys,
        Dictionary<string, StatPath> paths, List<string> roots)
    {
        if (keys.TryGetValue(scope.Id, out var known))
        {
            return known;
        }
        var root = string.Create(CultureInfo.InvariantCulture, $"{scope.Track}\u001f{scope.Name}");
        keys[scope.Id] = root;
        var parent = byId.TryGetValue(scope.ParentId, out var owner) && owner.Track == scope.Track
            ? PathKey(owner, byId, keys, paths, roots) : null;
        var key = parent is null ? root : $"{parent}\u001f{scope.Name}";
        if (!paths.ContainsKey(key))
        {
            AddPath(key, parent, scope, paths, roots);
        }
        return keys[scope.Id] = key;
    }

    /// <summary>Adds a path under its parent row; a cycle's provisional parent is not a row, so it becomes a root.</summary>
    static void AddPath(string key, string? parent, ProfileEvent scope, Dictionary<string, StatPath> paths,
        List<string> roots)
    {
        if (parent is not null && paths.TryGetValue(parent, out var owner))
        {
            paths[key] = new StatPath(scope.Name, scope.Track, owner.Depth + 1);
            owner.Children.Add(key);
        }
        else
        {
            paths[key] = new StatPath(scope.Name, scope.Track, 0);
            roots.Add(key);
        }
    }
}
