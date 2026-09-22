namespace Guinevere;

public partial class Gui
{
    readonly List<(int Z, int Sequence, LayoutNode Node)> _renderNodes = [];
    readonly List<LayoutNode> _clipAncestors = [];
    /// <summary>
    /// A property that represents the core rendering surface for graphical operations.
    /// </summary>
    /// <remarks>
    /// This property is an instance of the <see cref="SKCanvas"/> class and serves as the primary
    /// drawing canvas used for rendering visual elements in the GUI. It is initialized at the beginning
    /// of a frame and cleared at the end to ensure consistent frame rendering behavior.
    /// </remarks>
    public SKCanvas? Canvas { get; set; }

    /// <summary>
    /// A property that manages the application's time-related data and operations.
    /// </summary>
    /// <remarks>
    /// This property is an instance of the <see cref="Guinevere.Time"/> class and provides functionalities
    /// essential for tracking and updating time-dependent behaviors within the GUI. It is commonly used
    /// for tasks such as updating animations, calculating frame-related data like FPS, and handling input timing.
    /// </remarks>
    public Time Time { get; } = new();

    /// <summary>Capabilities published by the active platform integration.</summary>
    public PlatformCapabilities Platform { get; } = new();

    /// <summary>The active monotonic clock capability used by core and controls.</summary>
    public ITimeCapability Clock => Platform.Require<ITimeCapability>();

    /// <summary>Creates a GUI with its platform-independent clock capability.</summary>
    public Gui()
    {
        Platform.Register<ITimeCapability>(Time);
    }

    ControlPalette _controls = ControlPalette.Light;

    /// <summary>Fallback colors applied as independent values to each frame's root scope.</summary>
    public ControlPalette Controls
    {
        get => _controls;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _controls = value;
            if (LayoutNodeScopeStack.TryPeek(out var scope)) value.Apply(scope);
        }
    }

    /// <summary>Control colors and dimensions inherited by the current layout node.</summary>
    public ControlStyleValues ControlStyle => new(this);

    /// <summary>
    /// A property that provides an interface for handling window-specific operations.
    /// </summary>
    /// <remarks>
    /// The property is intended to associate a specific implementation of <see cref="IWindowHandler"/>
    /// with the GUI, enabling functionalities such as rendering, window management, and configuration.
    /// It must be set to a valid implementation before performing operations that require window handling.
    /// </remarks>
    IWindowHandler? _windowHandler;

    /// <summary>Legacy window shortcut. Prefer <c>Platform.Require&lt;IWindowHandler&gt;()</c>.</summary>
    public IWindowHandler WindowHandler
    {
        get => _windowHandler ?? Platform.Require<IWindowHandler>();
        set
        {
            _windowHandler = value;
            Platform.Register<IWindowHandler>(value);
        }
    }

    /// <summary>
    /// A property that provides the dimensions of the screen available for rendering.
    /// </summary>
    /// <remarks>
    /// The property returns a rectangle representing the visible area of the canvas
    /// or a zero-sized rectangle if the canvas is not initialized. The rectangle's width and height
    /// are derived from the <see cref="Canvas"/> object's local clip bounds.
    /// </remarks>
    public virtual Rect ScreenRect => new(0, 0,
        Canvas?.LocalClipBounds.Width ?? 0,
        Canvas?.LocalClipBounds.Height ?? 0);

    /// <summary>
    /// Initializes the GUI for a new frame by preparing the canvas, resetting layout states,
    /// and setting up root nodes for the layout system. Optionally sets the primary and icon fonts for rendering using Font wrappers.
    /// </summary>
    /// <param name="canvas">The canvas on which the GUI elements will be rendered for the current frame.</param>
    /// <param name="font">Optional parameter to set the default font for text rendering using Font wrapper.</param>
    /// <param name="fontIcon">Optional parameter to set the font for rendering icons using Font wrapper.</param>
    public void BeginFrame(SKCanvas canvas, Font? font = null, Font? fontIcon = null)
    {
        Canvas = canvas;
        if (Platform.TryGet<IAccessibilityCapability>(out var accessibility)) accessibility?.BeginFrame();

        // Events run against the previous frame's tree, before it is cleared, so listeners see what the user saw.
        DispatchInputEvents();
        _listenerCount = 0;
        _cursorNodeCount = 0;

        // Initialize focus management for the new frame
        BeginFrameFocus();

        // Clear the layout node stack to prevent accumulation
        LayoutNodeScopeStack.Clear();

        // Clear the previous tree completely
        if (RootNode is null)
        {
            RootNode = CreateRootNode(ScreenRect);
        }
        else
        {
            RootNode.ClearRoot();
            RegisterLayoutNodeScope(RootNode);
        }

        _controls.Apply(CurrentNodeScope);
        ControlMetrics.Apply(CurrentNodeScope);

        if (font is not null)
            SetTextFont(font);
        if (fontIcon is not null)
            SetIconFont(fontIcon);
    }

    /// <summary>
    /// Concludes the current GUI frame rendering process by releasing resources and performing cleanup tasks.
    /// Clears any completed drag operations and resets the current canvas to null.
    /// </summary>
    public void EndFrame()
    {
        // Process focus management and keyboard navigation
        ProcessFocusManagement();

        // Finalize focus management for the frame
        EndFrameFocus();

        ReleaseFinishedCapture();
        ClearCompletedDrags();
        ResolveDrag();
        ApplyCursorAndPointerMode();
        TrackPointerForNextFrame();
        if (Platform.TryGet<IAccessibilityCapability>(out var accessibility)) accessibility?.EndFrame();
        Canvas = null;
    }

    /// <summary>
    /// Sets the current rendering stage for the graphical user interface (GUI).
    /// Updates the pass value and resets certain layout node properties if applicable.
    /// </summary>
    /// <param name="newPass">The new rendering stage to assign, represented as a value of the <see cref="Pass"/> enumeration.</param>
    public void SetStage(Pass newPass)
    {
        Pass = newPass;
        if (RootNode is not null) RootNode!.Pass2NodeCount = 0;
    }

    /// <summary>
    /// Renders all layout nodes in the graphical user interface (GUI) by iterating
    /// through the hierarchical structure of layout nodes in order of their `z` index.
    /// </summary>
    /// <remarks>
    /// This method collects all layout nodes in a flat list, organizes them by their
    /// z-order value, and calls the Render method on their respective draw lists
    /// to perform the rendering. It requires a valid SKCanvas instance to be set
    /// as the `Canvas` property of the GUI.
    /// </remarks>
    /// <exception cref="NullReferenceException">
    /// Thrown if the `Canvas` property is null when the method is called.
    /// </exception>
    public void Render()
    {
        _renderNodes.Clear();
        NodeFlatList(RootNode!, _renderNodes);
        _renderNodes.Sort(static (left, right) =>
        {
            var zOrder = left.Z.CompareTo(right.Z);
            return zOrder != 0 ? zOrder : left.Sequence.CompareTo(right.Sequence);
        });

        foreach (var (_, _, node) in _renderNodes)
        {
            var restore = Canvas!.Save();
            ApplyAncestorClips(node, Canvas!);
            node.DrawList.Render(this, node, Canvas!);
            Canvas!.RestoreToCount(restore);
            node.Pass2NodeCount = 0;
        }
    }

    /// <summary>
    /// Re-applies the clip rect of every clipping ancestor of <paramref name="node"/> before it is
    /// drawn. Clips are recorded per node in a flat z-ordered render, so without this a scroll or
    /// <see cref="ClipContent"/> would leak its clip onto every node drawn after it.
    /// </summary>
    /// <remarks>
    /// A node that declares it escapes ancestor clips (a dialog or popup floating over a dock panel)
    /// skips every clip above it. That declaration has to stop the climb at the node that made it,
    /// not follow <see cref="LayoutNodeScope.Get{TValue}"/>'s usual cascade to every descendant —
    /// otherwise a scroll area nested inside the dialog would inherit the same "escapes everything"
    /// flag and its own <see cref="ClipContent"/> would never be applied to its rows either, and
    /// they would paint past its bounds instead of being clipped to it.
    /// </remarks>
    void ApplyAncestorClips(LayoutNode node, SKCanvas canvas)
    {
        if (node.Scope.HasLocal<LayoutNodeScopeEscapesAncestorClips>()
            && node.Scope.Get<LayoutNodeScopeEscapesAncestorClips>().Value)
            return;

        _clipAncestors.Clear();
        for (var a = node.Parent; a is not null; a = a.Parent)
        {
            _clipAncestors.Add(a);

            if (a.Scope.HasLocal<LayoutNodeScopeEscapesAncestorClips>()
                && a.Scope.Get<LayoutNodeScopeEscapesAncestorClips>().Value)
                break;
        }

        for (var i = _clipAncestors.Count - 1; i >= 0; i--)
        {
            var ancestor = _clipAncestors[i];
            if (!ancestor.Scope.HasLocal<LayoutNodeScopeIsClipped>()) continue;
            if (!ancestor.Scope.Get<LayoutNodeScopeIsClipped>().Value) continue;

            // The node's own rect, not its content box: clipping means "nothing outside this node",
            // and a scroll container draws its bar in the padding it reserved.
            var r = ancestor.Rect;
            if (r is { W: > 0, H: > 0 })
                canvas.ClipRect(r);
        }
    }

    void NodeFlatList(LayoutNode node, List<(int Z, int Sequence, LayoutNode Node)> list)
    {
        list.Add((node.Scope.Get<LayoutNodeScopeZIndex>().Value, list.Count, node));

        foreach (var child in node.Children)
            NodeFlatList(child, list);
    }

    /// <summary>
    /// Draws the window title bar, optionally showing or hiding it based on the specified parameter.
    /// </summary>
    /// <param name="show">A boolean value indicating whether the title bar should be visible. The default value is true.</param>
    public void DrawWindowTitlebar(bool show = true)
    {
        WindowHandler.DrawWindowTitlebar(show);
    }
}
