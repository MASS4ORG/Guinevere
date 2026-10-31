namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    GraphPoint? _marqueeStart;
    GraphBounds? _marquee;
    string[] _marqueeSelection = [];
    bool _marqueeAdd;
    bool _marqueeToggle;
    bool _duplicateDrag;
    bool _cancelledDrag;

    /// <summary>Opens application context actions at a world point after right-click focus and selection.</summary>
    public Action<GraphPoint>? ContextRequested { get; set; }

    void AttachEditingGestures(Gui gui, LayoutNode canvas)
    {
        canvas.On<PointerDownEvent>(e =>
        {
            if (e.Button == MouseButton.Right)
            {
                ContextRequested?.Invoke(Canvas.ScreenToWorld(e.Position));
                e.PreventDefault();
            }
        });
        canvas.On<DragStartEvent>(e =>
        {
            if (e.Button == MouseButton.Left && e.TargetId == canvas.Id)
            {
                _marqueeStart = Canvas.ScreenToWorld(e.Origin);
                _marqueeSelection = [.. Canvas.Navigation.SelectedKeys];
                _marqueeAdd = ShiftHeld(gui);
                _marqueeToggle = ControlHeld(gui);
                MoveMarquee(e);
            }
        });
        canvas.On<DragMoveEvent>(MoveMarquee);
        canvas.On<DragEndEvent>(e =>
        {
            if (e.Button == MouseButton.Left && _marqueeStart is not null)
            {
                MoveMarquee(e);
                _marqueeStart = null;
                _marquee = null;
                e.StopPropagation();
            }
        });
    }

    void MoveMarquee(DragEvent e)
    {
        if (_marqueeStart is not { } start || e.Button != MouseButton.Left)
        {
            return;
        }
        var end = Canvas.ScreenToWorld(e.Position);
        _marquee = new GraphBounds(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
            Math.Abs(start.X - end.X), Math.Abs(start.Y - end.Y));
        Canvas.Navigation.SetSelection(_marqueeSelection);
        SelectRectangle(_marquee.Value, _marqueeAdd, _marqueeToggle);
        e.StopPropagation();
    }

    void StartNodeDrag(Gui gui, DragEvent e)
    {
        _cancelledDrag = false;
        _duplicateDrag = gui.Input.IsKeyDown(KeyboardKey.LeftAlt) || gui.Input.IsKeyDown(KeyboardKey.RightAlt);
        BeginNodeDrag(e);
    }

    bool HandleEditingKey(Gui gui, KeyboardKey key)
    {
        if (key == KeyboardKey.Escape && CancelEditingGesture())
        {
            return true;
        }
        return ControlHeld(gui) ? HandleControlKey(gui, key) : HandleSelectionKey(gui, key);
    }

    bool CancelEditingGesture()
    {
        if (_dragStart is { } before)
        {
            foreach (var (id, position) in before)
            {
                _positions[id] = position;
            }
            _dragStart = null;
            _duplicateDrag = false;
            _cancelledDrag = true;
            return true;
        }
        if (_marqueeStart is not null)
        {
            Canvas.Navigation.SetSelection(_marqueeSelection);
            _marqueeStart = null;
            _marquee = null;
            return true;
        }
        return false;
    }

    bool HandleControlKey(Gui gui, KeyboardKey key)
    {
        switch (key)
        {
            case KeyboardKey.Z:
                if (ShiftHeld(gui))
                {
                    Redo();
                }
                else
                {
                    Undo();
                }
                return true;
            case KeyboardKey.Y:
                Redo();
                return true;
            case KeyboardKey.A:
                SelectAll();
                return true;
            case KeyboardKey.D:
                DuplicateSelection(new GraphPoint(GridSize, GridSize));
                return true;
            default:
                return ClipboardKey(gui, key);
        }
    }

    bool HandleSelectionKey(Gui gui, KeyboardKey key)
    {
        if (key is KeyboardKey.Delete or KeyboardKey.Backspace)
        {
            DeleteSelection();
            return true;
        }
        if (!ShiftHeld(gui))
        {
            return false;
        }
        var delta = key switch
        {
            KeyboardKey.Left => new GraphPoint(-GridSize, 0),
            KeyboardKey.Right => new GraphPoint(GridSize, 0),
            KeyboardKey.Up => new GraphPoint(0, -GridSize),
            KeyboardKey.Down => new GraphPoint(0, GridSize),
            _ => default
        };
        if (delta == default)
        {
            return false;
        }
        MoveSelection(delta);
        return true;
    }

    bool ClipboardKey(Gui gui, KeyboardKey key)
    {
        if (key is not (KeyboardKey.C or KeyboardKey.X or KeyboardKey.V))
        {
            return false;
        }
        if (!gui.Platform.TryGet<IClipboard>(out var clipboard) || clipboard is null)
        {
            ClipboardError("This host does not provide a clipboard.");
            return true;
        }
        switch (key)
        {
            case KeyboardKey.C:
                clipboard.SetClipboardText(CopySelection());
                break;
            case KeyboardKey.X:
                CutSelection(clipboard.SetClipboardText);
                break;
            default:
                Paste(clipboard.GetClipboardText(), new GraphPoint(GridSize, GridSize));
                break;
        }
        return true;
    }

    sealed class MarqueeOverlay(GraphDocumentView view) : IGraphOverlay
    {
        GraphBounds? _frame;
        /// <inheritdoc />
        public void Draw(Gui gui, GraphOverlayContext context)
        {
            if (context.Pass == Pass.Pass1Build)
            {
                _frame = view._marquee;
            }
            if (_frame is { } bounds)
            {
                var point = context.ToLocal(new GraphPoint(bounds.X, bounds.Y));
                gui.DrawStyledBox(gui.ResolvePart("graph-marquee"),
                    new Rect(point.X, point.Y, (float)(bounds.Width * context.Zoom), (float)(bounds.Height * context.Zoom)));
            }
        }
    }
}
