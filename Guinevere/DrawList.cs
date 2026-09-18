namespace Guinevere;

/// <summary>
/// Represents a list of drawable entries or operations which can be added, modified, or rendered to a canvas.
/// </summary>
public sealed class DrawList
{
    List<IDrawListEntry> _entries = [];

    /// <summary>
    /// Adds a drawable shape to the draw list.
    /// </summary>
    /// <param name="shape">The drawable object to add.</param>
    public void Add(IDrawable shape)
    {
        _entries.Add(new DrawableEntry(shape));
    }

    /// <summary>
    /// Adds a drawable shape to the beginning of the draw list.
    /// </summary>
    /// <param name="shape">The drawable object to prepend to the draw list.</param>
    public void Prepend(IDrawable shape)
    {
        _entries = [.. _entries.Prepend(new DrawableEntry(shape))];
    }

    /// <summary>
    /// Adds any draw list entry to the draw list.
    /// </summary>
    /// <param name="entry">The draw list entry to add.</param>
    public void Add(IDrawListEntry entry)
    {
        _entries.Add(entry);
    }

    /// <summary>
    /// Adds any draw list entry to the beginning of the draw list.
    /// </summary>
    /// <param name="entry">The draw list entry to prepend to the draw list.</param>
    public void Prepend(IDrawListEntry entry)
    {
        _entries = [.. _entries.Prepend(entry)];
    }

    /// <summary>
    /// Adds a clip operation to the draw list using the specified shape.
    /// </summary>
    public void AddClip(Shape shape, Vector2 positon)
    {
        _entries.Add(new ClipOperation(shape, positon));
    }

    /// <summary>
    /// Adds a clip operation to the draw list using the specified shape.
    /// </summary>
    public void AddClip(Rect rect)
    {
        _entries.Add(new ClipOperation(rect));
    }

    /// <summary>
    /// Renders all drawable entries in the list onto the specified canvas.
    /// </summary>
    /// <param name="gui">The GUI context used for rendering operations.</param>
    /// <param name="node">The layout node containing structural and styling information.</param>
    /// <param name="canvas">The canvas to render the drawable entries onto.</param>
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        foreach (var entry in _entries) entry.Execute(gui, node, canvas);
    }
}
