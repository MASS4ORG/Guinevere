namespace Guinevere;

/// <summary>
/// Represents a list of drawable entries or operations which can be added, modified, or rendered to a canvas.
/// </summary>
public sealed class DrawList
{
    readonly List<DrawCommand> _entries = [];
    List<PrimitiveCommand>? _primitives;

    /// <summary>Number of commands currently queued.</summary>
    public int Count => _entries.Count;

    /// <summary>Reserves storage for at least <paramref name="capacity"/> commands.</summary>
    public void EnsureCapacity(int capacity)
    {
        _entries.EnsureCapacity(capacity);
        (_primitives ??= []).EnsureCapacity(capacity);
    }

    /// <summary>Number of direct primitive submissions queued for each replay.</summary>
    public int PrimitiveCount => _primitives?.Count ?? 0;

    internal void Add(in PrimitiveCommand primitive, bool prepend = false)
    {
        var primitives = _primitives ??= [];
        var command = DrawCommand.Primitive(primitives.Count);
        primitives.Add(primitive);
        if (prepend) _entries.Insert(0, command);
        else _entries.Add(command);
    }

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

    /// <summary>
    /// The area the queued drawables paint, or <c>null</c> when one of them (a custom entry, or a paint with an image
    /// filter) cannot be bounded. Clips only shrink the result and are ignored.
    /// </summary>
    internal SKRect? InkBounds(LayoutNode node)
    {
        var ink = SKRect.Empty;
        foreach (var entry in _entries)
        {
            if (entry.IsClip) continue;
            if (entry.InkBounds(node, _primitives) is not { } bounds) return null;
            ink = Ink.Join(ink, bounds);
        }
        return ink;
    }

    /// <summary>Removes queued commands while retaining the allocated command buffer.</summary>
    public void Clear()
    {
        _entries.Clear();
        _primitives?.Clear();
    }

    /// <summary>
    /// Renders all drawable entries in the list onto the specified canvas.
    /// </summary>
    /// <param name="gui">The GUI context used for rendering operations.</param>
    /// <param name="node">The layout node containing structural and styling information.</param>
    /// <param name="canvas">The canvas to render the drawable entries onto.</param>
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        using var renderer = new PrimitiveRenderer();
        Render(gui, node, canvas, renderer);
    }

    internal void Render(Gui gui, LayoutNode node, SKCanvas canvas, PrimitiveRenderer renderer)
    {
        foreach (var entry in _entries) entry.Execute(gui, node, canvas, _primitives, renderer);
    }

    enum DrawCommandKind : byte
    {
        Drawable,
        ClipRect,
        ClipShape,
        Custom,
        Primitive
    }

    readonly struct DrawCommand
    {
        readonly DrawCommandKind _kind;
        readonly object _value;
        readonly Rect _rect;
        readonly int _primitiveIndex;

        DrawCommand(DrawCommandKind kind, object value, Rect rect = default, int primitiveIndex = 0)
        {
            _kind = kind;
            _value = value;
            _rect = rect;
            _primitiveIndex = primitiveIndex;
        }

        public bool IsClip => _kind is DrawCommandKind.ClipRect or DrawCommandKind.ClipShape;

        public SKRect? InkBounds(LayoutNode node, List<PrimitiveCommand>? primitives)
        {
            if (_kind == DrawCommandKind.Primitive) return primitives![_primitiveIndex].InkBounds();
            return _kind == DrawCommandKind.Drawable && _value is IInkBounds bounded ? bounded.InkBounds(node) : null;
        }

        public static DrawCommand Primitive(int index) =>
            new(DrawCommandKind.Primitive, null!, primitiveIndex: index);

        public static DrawCommand Drawable(IDrawable drawable) => new(DrawCommandKind.Drawable, drawable);
        public static DrawCommand Custom(IDrawListEntry entry) => new(DrawCommandKind.Custom, entry);
        public static DrawCommand Clip(Rect rect) => new(DrawCommandKind.ClipRect, null!, rect);
        public static DrawCommand Clip(Shape shape, Vector2 position) =>
            new(DrawCommandKind.ClipShape, shape, new Rect(position.X, position.Y));

        public void Execute(Gui gui, LayoutNode node, SKCanvas canvas, List<PrimitiveCommand>? primitives,
            PrimitiveRenderer renderer)
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
                    var matrix = canvas.TotalMatrix;
                    canvas.Translate(_rect.X, _rect.Y);
                    canvas.ClipPath(shape.Path);
                    canvas.SetMatrix(matrix);
                    break;
                case DrawCommandKind.Custom:
                    ((IDrawListEntry)_value).Execute(gui, node, canvas);
                    break;
                case DrawCommandKind.Primitive:
                    renderer.Render(primitives![_primitiveIndex], canvas);
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
