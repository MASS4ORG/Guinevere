namespace Guinevere;

public partial class Gui
{
    IControlSemanticsSink? _controlSemantics;

    /// <summary>
    /// Optional accessibility/automation capability. Backends that support semantics can publish the
    /// headless controls encountered during the render pass; unsupported backends leave it null.
    /// </summary>
    public IControlSemanticsSink? ControlSemantics
    {
        get => _controlSemantics ?? (Platform.TryGet<IControlSemanticsSink>(out var sink) ? sink : null);
        set
        {
            _controlSemantics = value;
            if (value is not null)
            {
                Platform.Register(value);
            }
            else
            {
                Platform.Remove<IControlSemanticsSink>();
            }
        }
    }

    IControlActivationSource? _controlActivation;

    /// <summary>
    /// Optional controller or command-routing capability. A source can activate the focused control
    /// without being added to the platform-neutral <see cref="IInputHandler"/> contract.
    /// </summary>
    public IControlActivationSource? ControlActivation
    {
        get => _controlActivation ?? (Platform.TryGet<IControlActivationSource>(out var source) ? source : null);
        set
        {
            _controlActivation = value;
            if (value is not null)
            {
                Platform.Register(value);
            }
            else
            {
                Platform.Remove<IControlActivationSource>();
            }
        }
    }

    /// <summary>
    /// Applies button behavior to the current node. Input detected in the render pass is delivered as
    /// <see cref="ControlBehaviorResult.Activated"/> in the next layout pass. This keeps application state
    /// and the node tree identical in both passes of a frame.
    /// </summary>
    public ControlBehaviorResult Pressable(ControlBehaviorOptions? options = null,
        Action<ControlVisualState>? render = null) =>
        Behavior(options ?? new ControlBehaviorOptions(), BehaviorKind.Press, repeatDelay: 0, repeatInterval: 0, render);

    /// <summary>Applies toggle behavior to the current node.</summary>
    public ControlBehaviorResult Toggleable(bool value, ControlBehaviorOptions? options = null,
        Action<ControlVisualState>? render = null) =>
        Behavior((options ?? new ControlBehaviorOptions()) with { Checked = value }, BehaviorKind.Toggle, 0, 0, render);

    /// <summary>Applies single-selection behavior to the current node.</summary>
    public ControlBehaviorResult Selectable(bool selected, ControlBehaviorOptions? options = null,
        Action<ControlVisualState>? render = null) =>
        Behavior((options ?? new ControlBehaviorOptions()) with { Selected = selected }, BehaviorKind.Select, 0, 0, render);

    /// <summary>Applies pointer-captured drag behavior to the current node.</summary>
    public ControlBehaviorResult Draggable(ControlBehaviorOptions? options = null,
        Action<ControlVisualState>? render = null) =>
        Behavior(options ?? new ControlBehaviorOptions(), BehaviorKind.Drag, 0, 0, render);

    /// <summary>Applies press behavior that repeats while held after the supplied delay.</summary>
    public ControlBehaviorResult Repeatable(ControlBehaviorOptions? options = null,
        float repeatDelay = 0.4f, float repeatInterval = 0.08f,
        Action<ControlVisualState>? render = null) =>
        Behavior(options ?? new ControlBehaviorOptions(), BehaviorKind.Repeat,
            Math.Max(0, repeatDelay), Math.Max(0.001f, repeatInterval), render);

    ControlBehaviorResult Behavior(ControlBehaviorOptions options, BehaviorKind kind,
        float repeatDelay, float repeatInterval, Action<ControlVisualState>? render)
    {
        var id = CurrentNode.Id;
        var behavior = ControlState(id, static () => new HeadlessBehaviorState());

        if (Pass == Pass.Pass1Build)
        {
            behavior.FrameState = ComposeState(options, behavior.Hovered, behavior.Pressed,
                behavior.Focused, behavior.Dragging);
            behavior.FrameActivated = behavior.PendingActivated;
            behavior.FrameRepeated = behavior.PendingRepeated;
            behavior.FrameDrag = behavior.PendingDrag;
            behavior.PendingActivated = false;
            behavior.PendingRepeated = false;
            behavior.PendingDrag = null;
        }

        var result = new ControlBehaviorResult(
            behavior.FrameState,
            Pass == Pass.Pass1Build && behavior.FrameActivated,
            Pass == Pass.Pass1Build && behavior.FrameRepeated,
            Pass == Pass.Pass1Build ? behavior.FrameDrag : null);

        render?.Invoke(result.State);

        if (Pass != Pass.Pass2Render)
        {
            return result;
        }

        UpdateBehavior(behavior, options, kind, repeatDelay, repeatInterval);
        return result;
    }

    void UpdateBehavior(HeadlessBehaviorState behavior, ControlBehaviorOptions options, BehaviorKind kind,
        float repeatDelay, float repeatInterval)
    {
        if (options.Focusable)
        {
            RegisterFocusable(options.Enabled, options.Enabled);
        }

        var interaction = ReadBehaviorInteraction(options);
        if (interaction.Activated)
        {
            behavior.PendingActivated = true;
        }

        behavior.Hovered = interaction.Hovered;
        behavior.Pressed = interaction.Pressed;
        behavior.Focused = interaction.Focused;
        behavior.Dragging = false;
        UpdateBehaviorDrag(behavior, options, kind);
        if (kind == BehaviorKind.Repeat)
        {
            UpdateRepeat(behavior, interaction.Pressed, repeatDelay, repeatInterval);
        }

        PublishBehaviorSemantics(behavior, options);
    }

    BehaviorInteraction ReadBehaviorInteraction(ControlBehaviorOptions options)
    {
        var focused = options.Focusable && HasFocus();
        if (!options.Enabled)
        {
            return new BehaviorInteraction(false, false, focused, false);
        }

        var interactable = GetInteractable();
        var hovered = interactable.OnHover();
        var pressed = interactable.OnHold();
        var pointerActivated = interactable.OnClick();
        var activated = pointerActivated || focused && IsBehaviorActivationPressed(options);
        if (pointerActivated)
        {
            RequestFocus(FocusReason.Mouse);
        }

        return new BehaviorInteraction(hovered, pressed, focused || pointerActivated, activated);
    }

    bool IsBehaviorActivationPressed(ControlBehaviorOptions options) =>
        Input.IsKeyPressed(options.ActivationKey) || Input.IsKeyPressed(KeyboardKey.Enter)
        || ControlActivation?.IsActivated(CurrentNode.Id) == true || IsSemanticActionPressed(SemanticAction.Accept);

    void UpdateBehaviorDrag(HeadlessBehaviorState behavior, ControlBehaviorOptions options, BehaviorKind kind)
    {
        if (kind == BehaviorKind.Drag && options.Enabled && GetInteractable().OnDrag(out var drag))
        {
            behavior.Dragging = true;
            behavior.PendingDrag = drag;
        }

    }

    void PublishBehaviorSemantics(HeadlessBehaviorState behavior, ControlBehaviorOptions options)
    {
        var semantics = new ControlSemantics(CurrentNode.Id, options.Role, options.Label, options.Value,
            CurrentNode.Rect, ComposeState(options, behavior.Hovered, behavior.Pressed, behavior.Focused, behavior.Dragging));
        if (ControlSemantics is { } sink)
        {
            sink.Publish(semantics);
        }
        else if (Platform.TryGet<IAccessibilityCapability>(out var accessibility))
        {
            accessibility?.Publish(semantics);
        }
    }

    readonly record struct BehaviorInteraction(bool Hovered, bool Pressed, bool Focused, bool Activated);

    void UpdateRepeat(HeadlessBehaviorState behavior, bool pressed, float delay, float interval)
    {
        if (!pressed)
        {
            behavior.HoldStarted = null;
            behavior.LastRepeat = 0;
            return;
        }

        behavior.HoldStarted ??= Clock.Elapsed;
        var heldFor = Clock.Elapsed - behavior.HoldStarted.Value;
        if (heldFor < delay || Clock.Elapsed - behavior.LastRepeat < interval)
        {
            return;
        }

        behavior.LastRepeat = Clock.Elapsed;
        behavior.PendingRepeated = true;
    }

    static ControlVisualState ComposeState(ControlBehaviorOptions options, bool hovered,
        bool pressed, bool focused, bool dragging)
    {
        var state = ControlVisualState.None;
        if (!options.Enabled)
        {
            state |= ControlVisualState.Disabled;
        }

        if (focused)
        {
            state |= ControlVisualState.Focused;
        }

        if (hovered)
        {
            state |= ControlVisualState.Hovered;
        }

        if (pressed)
        {
            state |= ControlVisualState.Pressed;
        }

        if (options.Checked)
        {
            state |= ControlVisualState.Checked;
        }

        if (options.Selected)
        {
            state |= ControlVisualState.Selected;
        }

        if (dragging)
        {
            state |= ControlVisualState.Dragging;
        }

        return state;
    }

    enum BehaviorKind { Press, Toggle, Select, Drag, Repeat }

    sealed class HeadlessBehaviorState
    {
        public bool Hovered { get; set; }
        public bool Pressed { get; set; }
        public bool Focused { get; set; }
        public bool Dragging { get; set; }
        public bool PendingActivated { get; set; }
        public bool PendingRepeated { get; set; }
        public DragArgs? PendingDrag { get; set; }
        public ControlVisualState FrameState { get; set; }
        public bool FrameActivated { get; set; }
        public bool FrameRepeated { get; set; }
        public DragArgs? FrameDrag { get; set; }
        public float? HoldStarted { get; set; }
        public float LastRepeat { get; set; }
    }
}
