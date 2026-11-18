namespace Guinevere;

/// <summary>
/// What a table remembers between frames: the sort order its header shows, and the viewport the last
/// frame measured, which both passes of the next frame build their visible window from.
/// </summary>
public sealed class TableState
{
    readonly Dictionary<string, (float FontSize, float Width)> _autoWidths = [];

    /// <summary>
    /// The order the header marks. The table only draws it: the caller sets it when it applies a sort to
    /// the source, because a table over a virtual dataset cannot sort rows it never holds.
    /// </summary>
    public TableSort? Sort { get; set; }

    /// <summary>The column list last checked for duplicate ids, so a list shared between frames is checked once.</summary>
    internal IReadOnlyList<TableColumn>? CheckedColumns { get; set; }

    /// <summary>Horizontal scroll offset of the body, which the sticky header follows.</summary>
    internal float FrameScrollX { get; set; }

    /// <summary>Vertical scroll offset of the body.</summary>
    internal float FrameScrollY { get; set; }

    /// <summary>The body's viewport width inside its padding; columns share it.</summary>
    internal float FrameViewportWidth { get; set; } = 800f;

    /// <summary>The body's viewport height; rows are built to fill it.</summary>
    internal float FrameViewportHeight { get; set; } = 600f;

    /// <summary>A column's header-sized width, measured once per font size because a definition never changes.</summary>
    internal float AutoWidth(TableColumn column, float fontSize, Func<float> measure)
    {
        if (_autoWidths.TryGetValue(column.Id, out var cached) && cached.FontSize == fontSize)
        {
            return cached.Width;
        }

        var width = measure();
        _autoWidths[column.Id] = (fontSize, width);
        return width;
    }
}
