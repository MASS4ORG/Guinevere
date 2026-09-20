namespace Guinevere;

/// <summary>
/// Represents a list of drawable entries or operations which can be added, modified, or rendered to a canvas.
/// </summary>
public sealed class DrawList
{
    readonly List<DrawCommand> _entries = [];

    /// <summary>Number of commands currently queued.</summary>
    public int Count => _entries.Count;

    /// <summary>Reserves storage for at least <paramref name="capacity"/> commands.</summary>
    public void EnsureCapacity(int capacity) => _entries.EnsureCapacity(capacity);

    /// <summary>
    /// Adds a drawable shape to the draw list.
    /// </summary>
    /// <param name="shape">The drawable object to add.</param>
    public void Add(IDrawable shape)
    {
        _entries.Add(DrawCommand.Drawable(shape));
    }

    /// <summary>
    /// Adds a drawable shape to the beginning of the draw list.
    /// </summary>
    /// <param name="shape">The drawable object to prepend to the draw list.</param>
    public void Prepend(IDrawable shape)
    {
        _entries.Insert(0, DrawCommand.Drawable(shape));
    }

    /// <summary>
    /// Adds any draw list entry to the draw list.
    /// </summary>
    /// <param name="entry">The draw list entry to add.</param>
    public void Add(IDrawListEntry entry)
    {
        _entries.Add(DrawCommand.Custom(entry));
    }

    /// <summary>
    /// Adds any draw list entry to the beginning of the draw list.
    /// </summary>
    /// <param name="entry">The draw list entry to prepend to the draw list.</param>
    public void Prepend(IDrawListEntry entry)
    {
        _entries.Insert(0, DrawCommand.Custom(entry));
    }

    /// <summary>
    /// Adds a clip operation to the draw list using the specified shape.
    /// </summary>
    public void AddClip(Shape shape, Vector2 positon)
    {
        _entries.Add(DrawCommand.Clip(shape, positon));
    }

    /// <summary>
    /// Adds a clip operation to the draw list using the specified shape.
    /// </summary>
    public void AddClip(Rect rect)
    {
        _entries.Add(DrawCommand.Clip(rect));
    }

    /// <summary>Removes queued commands while retaining the allocated command buffer.</summary>
    public void Clear() => _entries.Clear();

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

    enum DrawCommandKind : byte
    {
        Drawable,
        ClipRect,
        ClipShape,
        Custom
    }

    readonly struct DrawCommand
    {
        readonly DrawCommandKind _kind;
        readonly object _value;
        readonly Rect _rect;
        readonly Vector2 _position;

        DrawCommand(DrawCommandKind kind, object value, Rect rect = default, Vector2 position = default)
        {
            _kind = kind;
            _value = value;
            _rect = rect;
            _position = position;
        }

        public static DrawCommand Drawable(IDrawable drawable) => new(DrawCommandKind.Drawable, drawable);
        public static DrawCommand Custom(IDrawListEntry entry) => new(DrawCommandKind.Custom, entry);
        public static DrawCommand Clip(Rect rect) => new(DrawCommandKind.ClipRect, null!, rect);
        public static DrawCommand Clip(Shape shape, Vector2 position) =>
            new(DrawCommandKind.ClipShape, shape, position: position);

        public void Execute(Gui gui, LayoutNode node, SKCanvas canvas)
        {
            switch (_kind)
            {
                case DrawCommandKind.Drawable:
                    ((IDrawable)_value).Render(gui, node, canvas);
                    break;
                case DrawCommandKind.ClipRect:
                    ExecuteClip(gui, node, canvas, _rect);
                    break;
                case DrawCommandKind.ClipShape:
                    canvas.Save();
                    var shape = (Shape)_value;
                    var positioned = new ShapePos(shape.Path, shape.Paint, _position);
                    canvas.ClipPath(positioned.Path);
                    break;
                case DrawCommandKind.Custom:
                    ((IDrawListEntry)_value).Execute(gui, node, canvas);
                    break;
            }
        }

        static void ExecuteClip(Gui gui, LayoutNode node, SKCanvas canvas, Rect rect)
        {
            canvas.Save();
            if (rect.W <= 0 || rect.H <= 0) return;

            var scrollState = gui.GetScrollState(node.Id);
            if (scrollState != null && (scrollState.IsScrollingX || scrollState.IsScrollingY))
            {
                if (node.Rect is { W: > 0, H: > 0 } viewportRect) canvas.ClipRect(viewportRect);
                return;
            }

            canvas.ClipRect(rect);
        }
    }
}
