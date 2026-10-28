namespace Guinevere.Graphs;

public sealed partial class GraphCanvas
{
    static readonly Dictionary<KeyboardKey, Action<GraphCanvas>> ActionKeys = new()
    {
        [KeyboardKey.Equal] = static canvas => canvas.Camera.ZoomAt(canvas._viewportSize / 2, 1.25),
        [KeyboardKey.KeyPadAdd] = static canvas => canvas.Camera.ZoomAt(canvas._viewportSize / 2, 1.25),
        [KeyboardKey.Minus] = static canvas => canvas.Camera.ZoomAt(canvas._viewportSize / 2, 0.8),
        [KeyboardKey.KeyPadSubtract] = static canvas => canvas.Camera.ZoomAt(canvas._viewportSize / 2, 0.8),
        [KeyboardKey.D0] = static canvas => canvas.Camera.Reset(),
        [KeyboardKey.Home] = static canvas => canvas.FitAll(),
        [KeyboardKey.F] = static canvas => canvas.FitSelection(),
        [KeyboardKey.Enter] = static canvas => canvas.Navigation.Activate(),
        [KeyboardKey.Space] = static canvas => canvas.Navigation.ToggleSelection(),
        [KeyboardKey.Delete] = static canvas => canvas.Navigation.Delete(),
        [KeyboardKey.Backspace] = static canvas => canvas.Navigation.Delete()
    };

    readonly Queue<Action> _pendingInput = new();

    void AttachInput(Gui gui, LayoutNode canvas)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            while (_pendingInput.TryDequeue(out var action))
            {
                action();
            }
        }
        canvas.On<PointerDownEvent>(e =>
        {
            if (e.TargetId == canvas.Id || e.Button == MouseButton.Middle)
            {
                gui.Focus.RequestFocus(canvas.Id, FocusReason.Mouse);
            }
        });
        canvas.On<ScrollEvent>(e =>
        {
            var anchor = canvas.ScreenToLocal(e.Position) - canvas.Rect.Position;
            Camera.ZoomAt(anchor, Math.Pow(1.15, Math.Clamp(e.Delta, -32, 32)));
            e.PreventDefault();
            e.StopPropagation();
        });
        canvas.On<DragStartEvent>(e => PanDrag(canvas, e));
        canvas.On<DragMoveEvent>(e => PanDrag(canvas, e));
        canvas.On<KeyDownEvent>(e =>
        {
            if (e.TargetId == canvas.Id && HandleKey(gui, e.Key))
            {
                e.PreventDefault();
                e.StopPropagation();
            }
        });
    }

    void PanDrag(LayoutNode canvas, DragEvent e)
    {
        if (e.Button != MouseButton.Middle)
        {
            return;
        }
        var current = canvas.ScreenToLocal(e.Position);
        var previous = canvas.ScreenToLocal(e.Position - e.FrameDelta);
        Camera.Pan(current - previous);
        e.StopPropagation();
    }

    bool HandleKey(Gui gui, KeyboardKey key)
    {
        var control = ModifierHeld(gui, KeyboardKey.LeftControl, KeyboardKey.RightControl);
        var shift = ModifierHeld(gui, KeyboardKey.LeftShift, KeyboardKey.RightShift);
        if (HandleSearchKey(key, control, shift))
        {
            return true;
        }
        if (TryDirection(key, out var direction))
        {
            HandleDirection(direction, control, ModifierHeld(gui, KeyboardKey.LeftAlt, KeyboardKey.RightAlt));
            return true;
        }
        return HandleActionKey(key, shift);
    }

    void HandleDirection(FocusDirection direction, bool control, bool alt)
    {
        if (control)
        {
            Camera.Pan(direction switch
            {
                FocusDirection.Left => new Vector2(48, 0),
                FocusDirection.Right => new Vector2(-48, 0),
                FocusDirection.Up => new Vector2(0, 48),
                _ => new Vector2(0, -48)
            });
        }
        else if (alt)
        {
            Navigation.MoveEdge(direction is FocusDirection.Left or FocusDirection.Up);
        }
        else
        {
            Navigation.Move(direction);
        }
    }

    bool HandleActionKey(KeyboardKey key, bool shift)
    {
        if (key == KeyboardKey.Tab)
        {
            return Navigation.FocusedElement is { Role: GraphElementRole.Port } && Navigation.MovePort(shift);
        }
        if (key == KeyboardKey.Escape)
        {
            return Navigation.Back();
        }
        if (!ActionKeys.TryGetValue(key, out var action))
        {
            return false;
        }
        action(this);
        return true;
    }

    bool HandleSearchKey(KeyboardKey key, bool control, bool shift)
    {
        if (control && key == KeyboardKey.K)
        {
            Navigation.Search(GraphSearchKind.Commands);
            return true;
        }
        if (key == KeyboardKey.Insert || shift && key == KeyboardKey.A)
        {
            Navigation.Search(GraphSearchKind.CreateNode);
            return true;
        }
        return false;
    }

    void ReadSemanticInput(Gui gui)
    {
        if (!gui.HasFocus())
        {
            return;
        }
        ReadDirectionalInput(gui);
        QueueInput(gui, IsGraphActivated(gui), () => Navigation.Activate());
        QueueInput(gui, gui.IsSemanticActionPressed(SemanticAction.Cancel), () => Navigation.Back());
    }

    void ReadDirectionalInput(Gui gui)
    {
        foreach (var direction in new[] { FocusDirection.Left, FocusDirection.Right,
                     FocusDirection.Up, FocusDirection.Down })
        {
            var action = direction switch
            {
                FocusDirection.Left => SemanticAction.Left,
                FocusDirection.Right => SemanticAction.Right,
                FocusDirection.Up => SemanticAction.Up,
                _ => SemanticAction.Down
            };
            QueueInput(gui, gui.IsSemanticActionPressed(action), () => Navigation.Move(direction));
        }
    }

    void QueueInput(Gui gui, bool pressed, Action action)
    {
        if (pressed)
        {
            _pendingInput.Enqueue(action);
            gui.RequestFrame();
        }
    }

    static bool IsGraphActivated(Gui gui) => gui.IsSemanticActionPressed(SemanticAction.Accept)
        || gui.ControlActivation?.IsActivated(gui.CurrentNode.Id) == true;

    static bool ModifierHeld(Gui gui, KeyboardKey left, KeyboardKey right) =>
        gui.Input.IsKeyDown(left) || gui.Input.IsKeyDown(right);

    static bool TryDirection(KeyboardKey key, out FocusDirection direction)
    {
        direction = key switch
        {
            KeyboardKey.Left => FocusDirection.Left,
            KeyboardKey.Right => FocusDirection.Right,
            KeyboardKey.Up => FocusDirection.Up,
            KeyboardKey.Down => FocusDirection.Down,
            _ => FocusDirection.Next
        };
        return direction != FocusDirection.Next;
    }
}
