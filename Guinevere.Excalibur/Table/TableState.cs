namespace Guinevere;

/// <summary>
/// What a table remembers between frames: the sort order its header shows, and the viewport the last
/// frame measured, which both passes of the next frame build their visible window from.
/// </summary>
public sealed class TableState
{
    /// <summary>
    /// The order the header marks. The table only draws it: the caller sets it when it applies a sort to
    /// the source, because a table over a virtual dataset cannot sort rows it never holds.
    /// </summary>
    public TableSort? Sort { get; set; }

    /// <summary>Layout, ids and text the table derives from its inputs and reuses until they change.</summary>
    internal TableCache Cache { get; } = new();

    /// <summary>Horizontal scroll offset of the body, which the sticky header follows.</summary>
    internal float FrameScrollX { get; set; }

    /// <summary>Vertical scroll offset of the body.</summary>
    internal float FrameScrollY { get; set; }

    /// <summary>The body's viewport width inside its padding; columns share it.</summary>
    internal float FrameViewportWidth { get; set; } = 800f;

    /// <summary>The body's viewport height; rows are built to fill it.</summary>
    internal float FrameViewportHeight { get; set; } = 600f;
}
