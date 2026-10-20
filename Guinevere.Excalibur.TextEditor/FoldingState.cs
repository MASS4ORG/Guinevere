namespace Guinevere;

/// <summary>
/// Folded regions and the mapping between visible rows and buffer lines. Hidden lines are kept as merged intervals,
/// so the mapping costs O(folds) instead of O(lines).
/// </summary>
sealed class FoldingState
{
    readonly SortedDictionary<int, int> _folded = [];
    List<(int First, int Last)> _hidden = [];

    /// <summary>The foldable ranges last reported by the provider, sorted by start line.</summary>
    internal IReadOnlyList<FoldingRange> Ranges { get; private set; } = [];

    /// <summary>The folded ranges as start line to end line.</summary>
    internal IReadOnlyDictionary<int, int> Folded => _folded;

    /// <summary>Replaces the foldable ranges, keeping folds whose start line still starts a range.</summary>
    internal void SetRanges(IReadOnlyList<FoldingRange> ranges)
    {
        Ranges = Normalize(ranges);
        foreach (var start in _folded.Keys.ToList())
        {
            if (RangeAt(start) is { } range)
            {
                _folded[start] = range.EndLine;
            }
            else
            {
                _folded.Remove(start);
            }
        }

        Rebuild();
    }

    static FoldingRange[] Normalize(IReadOnlyList<FoldingRange> ranges) =>
        [.. ranges.Where(range => range.StartLine >= 0 && range.EndLine > range.StartLine)
            .OrderBy(range => range.StartLine).ThenByDescending(range => range.EndLine)
            .DistinctBy(range => range.StartLine)];

    /// <summary>The foldable range starting on a line, or null.</summary>
    internal FoldingRange? RangeAt(int line)
    {
        var low = 0;
        var high = Ranges.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (Ranges[mid].StartLine == line)
            {
                return Ranges[mid];
            }

            if (Ranges[mid].StartLine < line)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return null;
    }

    /// <summary>Folds or unfolds the range starting on a line; returns whether one starts there.</summary>
    internal bool Toggle(int line)
    {
        if (_folded.Remove(line))
        {
            Rebuild();
            return true;
        }

        if (RangeAt(line) is not { } range)
        {
            return false;
        }

        _folded[line] = range.EndLine;
        Rebuild();
        return true;
    }

    /// <summary>Unfolds every region hiding a line, so a caret moved there becomes visible.</summary>
    internal bool Reveal(int line)
    {
        var removed = false;
        foreach (var (start, end) in _folded.ToList())
        {
            if (line > start && line <= end)
            {
                removed |= _folded.Remove(start);
            }
        }

        if (removed)
        {
            Rebuild();
        }

        return removed;
    }

    /// <summary>Unfolds everything.</summary>
    internal void Clear()
    {
        _folded.Clear();
        Rebuild();
    }

    /// <summary>Keeps folds on the same text after lines are added or removed below <paramref name="line"/>.</summary>
    internal void Shift(int line, int delta)
    {
        if (delta == 0 || _folded.Count == 0)
        {
            return;
        }

        var moved = new List<(int Start, int End)>();
        foreach (var (start, end) in _folded)
        {
            if (ShiftRange(start, end, line, delta) is { } range)
            {
                moved.Add(range);
            }
        }

        _folded.Clear();
        foreach (var (start, end) in moved)
        {
            _folded[start] = end;
        }

        Rebuild();
    }

    static (int Start, int End)? ShiftRange(int start, int end, int line, int delta)
    {
        if (delta < 0 && start > line && start <= (long)line - delta)
        {
            return null;
        }
        var newStart = start > line ? start + delta : start;
        var newEnd = end >= line ? end + delta : end;
        return newEnd > newStart && newStart >= 0 ? (newStart, newEnd) : null;
    }

    /// <summary>Whether a line is inside a folded region.</summary>
    internal bool IsHidden(int line)
    {
        foreach (var (first, last) in _hidden)
        {
            if (line < first)
            {
                return false;
            }

            if (line <= last)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Number of visible rows for a line count.</summary>
    internal int RowCount(int lineCount)
    {
        var hidden = 0;
        foreach (var (first, last) in _hidden)
        {
            if (first < lineCount)
            {
                hidden += Math.Min(last, lineCount - 1) - first + 1;
            }
        }

        return lineCount - hidden;
    }

    /// <summary>The buffer line shown on a row.</summary>
    internal int LineOfRow(int row)
    {
        var line = row;
        foreach (var (first, last) in _hidden)
        {
            if (first > line)
            {
                break;
            }

            line += last - first + 1;
        }

        return line;
    }

    /// <summary>The row showing a line, or the row of the fold hiding it.</summary>
    internal int RowOfLine(int line)
    {
        var row = line;
        foreach (var (first, last) in _hidden)
        {
            if (first > line)
            {
                break;
            }

            row -= Math.Min(line, last) - first + 1;
        }

        return row;
    }

    void Rebuild()
    {
        var hidden = new List<(int First, int Last)>();
        foreach (var (start, end) in _folded)
        {
            var first = start + 1;
            if (hidden.Count > 0 && first <= hidden[^1].Last + 1)
            {
                hidden[^1] = (hidden[^1].First, Math.Max(hidden[^1].Last, end));
            }
            else
            {
                hidden.Add((first, end));
            }
        }

        _hidden = hidden;
    }
}
