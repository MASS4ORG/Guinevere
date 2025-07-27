namespace Guinevere;

public partial class Gui
{
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
    public Time Time { get; init; } = new();

    /// <summary>
    /// A property that provides an interface for handling window-specific operations.
    /// </summary>
    /// <remarks>
    /// The property is intended to associate a specific implementation of <see cref="IWindowHandler"/>
    /// with the GUI, enabling functionalities such as rendering, window management, and configuration.
    /// It must be set to a valid implementation before performing operations that require window handling.
    /// </remarks>
    public IWindowHandler WindowHandler { get; set; } = null!;

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

        ClearCompletedDrags();
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
        var flatList = new List<(int z, LayoutNode node)>();
        NodeFlatList(RootNode!, flatList);

        foreach (var (_, node) in flatList
                     .OrderBy(value => value.z)
                )
        {
            node.DrawList.Render(this, node, Canvas!);
            node.Pass2NodeCount = 0;
        }
    }

    private void NodeFlatList(LayoutNode node, List<(int, LayoutNode)> list)
    {
        list.Add((node.Scope.Get<LayoutNodeScopeZIndex>().Value, node));

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
