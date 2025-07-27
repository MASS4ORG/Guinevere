using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Represents a layout node in the GUI framework's hierarchical layout system.
/// A layout node manages positioning, sizing, styling, and rendering of UI elements,
/// providing a flexible system for building complex user interfaces.
/// </summary>
/// <remarks>
/// Layout nodes form the foundation of the GUI framework's layout system. Each node can contain
/// child nodes, creating a hierarchical structure that enables complex UI layouts. Nodes handle
/// both the layout calculation (sizing and positioning) and rendering phases of the GUI pipeline.
/// </remarks>
public partial class LayoutNode : IDisposable
{
    private readonly Gui _gui;
    private readonly LayoutNode? _parent;
    private Rect _rect = Rect.Zero;

    /// <summary>
    /// Represents the collection of child nodes directly associated with this <see cref="LayoutNode"/>.
    /// </summary>
    /// <remarks>
    /// Child nodes define the hierarchical structure of the GUI layout by attaching subordinate
    /// <see cref="LayoutNode"/> instances to this node. These children can be manipulated to
    /// build or modify the layout programmatically. The collection is mutable and allows for
    /// dynamic addition and removal of nodes.
    /// </remarks>
    public readonly List<LayoutNode> ChildNodes = new();

    /// <summary>
    /// Gets the unique identifier for this <see cref="LayoutNode"/>.
    /// </summary>
    /// <remarks>
    /// The identifier is assigned during the node's creation and is immutable.
    /// It is used to uniquely identify a node within the GUI layout structure,
    /// allowing interactions and traversal methods to reliably target specific nodes.
    /// </remarks>
    public string Id { get; private set; }

    /// <summary>
    /// Gets the scope management object associated with this layout node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Scope"/> encapsulates contextual settings and state unique to a specific
    /// <see cref="LayoutNode"/> instance, including visual presentation attributes and rendering priorities.
    /// </para>
    /// <para>
    /// It provides utility to modify properties, manage z-order, and handle entry/exit operations
    /// when interacting with nested layouts or GUI composition methods.
    /// </para>
    /// <para>
    /// The lifespan of a <see cref="Scope"/> is generally tied to its owning <see cref="LayoutNode"/>,
    /// ensuring consistent behavior for scope-driven mechanisms or hierarchical structure handling.
    /// </para>
    /// </remarks>
    public readonly LayoutNodeScope Scope;

    /// <summary>
    /// Gets or sets the list of graphical draw commands associated with this layout node.
    /// </summary>
    /// <remarks>
    /// This property stores and manages rendering instructions for the node, such as shapes, clip areas,
    /// text, images, and other visual elements. The draw list is populated during the render pass and
    /// is used to generate the final visual output for this node.
    /// </remarks>
    public DrawList DrawList { get; set; } = new();

    /// <summary>
    /// Gets or sets the node count used during the second pass of layout processing.
    /// </summary>
    /// <remarks>
    /// This property tracks the number of layout nodes processed or created during the render pass.
    /// It is primarily utilized to manage and ensure the proper organization or identification
    /// of nodes within a layout phase, particularly when working with nested or hierarchical structures.
    /// </remarks>
    public int Pass2NodeCount { get; set; }

    /// <summary>
    /// Represents the layout style configuration for the <see cref="LayoutNode"/>.
    /// </summary>
    /// <remarks>
    /// This variable defines the visual and spatial characteristics applied to a <see cref="LayoutNode"/>,
    /// such as margins, padding, and other style-related properties. It directly influences how the
    /// layout node is rendered and interacts with its surroundings.
    /// </remarks>
    public LayoutStyle Style;

    /// <summary>
    /// Gets a read-only list of child nodes contained within this layout node.
    /// </summary>
    /// <value>
    /// An <see cref="IReadOnlyList{T}"/> of <see cref="LayoutNode"/> objects representing the child nodes.
    /// </value>
    public IReadOnlyList<LayoutNode> Children => ChildNodes.AsReadOnly();

    /// <summary>
    /// Gets the parent node of this layout node, or null if this is a root node.
    /// </summary>
    /// <value>
    /// The parent <see cref="LayoutNode"/>, or null if this node has no parent.
    /// </value>
    public LayoutNode? Parent => _parent;

    /// <summary>
    /// Gets the computed rectangle bounds of this layout node.
    /// </summary>
    /// <value>
    /// A <see cref="Rect"/> representing the position and size of this node after layout calculation.
    /// </value>
    public Rect Rect => _rect;

    /// <summary>
    /// Gets the center point of this layout node.
    /// </summary>
    /// <value>
    /// A <see cref="Vector2"/> representing the center coordinates of this node's rectangle.
    /// </value>
    public Vector2 Center => new(Rect.X + Rect.W / 2, Rect.Y + Rect.H / 2);

    /// <summary>
    /// Gets the inner rectangle of this layout node, accounting for padding.
    /// </summary>
    /// <value>
    /// A <see cref="Rect"/> representing the content area inside the padding boundaries.
    /// This is the area available for child content or drawing operations.
    /// </value>
    public Rect InnerRect => new(
        Rect.X + Style.PaddingLeft,
        Rect.Y + Style.PaddingTop,
        Rect.W - (Style.PaddingLeft + Style.PaddingRight),
        Rect.H - (Style.PaddingTop + Style.PaddingBottom)
    );

    /// <summary>
    /// Gets the outer rectangle of this layout node, including margins.
    /// </summary>
    /// <value>
    /// A <see cref="Rect"/> representing the total space occupied by this node including its margins.
    /// This represents the full footprint of the node in the layout.
    /// </value>
    public Rect OuterRect => new(
        Rect.X - Style.MarginLeft,
        Rect.Y - Style.MarginTop,
        Rect.W + (Style.MarginLeft + Style.MarginRight),
        Rect.H + (Style.MarginTop + Style.MarginBottom)
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="LayoutNode"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for this layout node.</param>
    /// <param name="gui">The GUI instance that owns this node.</param>
    /// <param name="parent">The parent node, or null if this is a root node.</param>
    /// <param name="width">The initial width of the node, or null to use default sizing.</param>
    /// <param name="height">The initial height of the node, or null to use default sizing.</param>
    /// <remarks>
    /// When width or height is 0, the node will be configured to expand to fill available space
    /// in that dimension. This constructor also creates the associated <see cref="Scope"/>
    /// for managing the node's contextual state.
    /// </remarks>
    public LayoutNode(string? id, Gui gui, LayoutNode? parent, float? width = null, float? height = null)
    {
        _gui = gui;
        _parent = parent;
        Style = LayoutStyle.Default;
        if (width.HasValue)
            Style.Width = width.Value;
        if (height.HasValue)
            Style.Height = height.Value;
        Id = id ?? _gui.NodeId(string.Empty, 0);
        Scope = new LayoutNodeScope(gui, this);
        if (width == 0) ExpandWidth();

        if (height == 0) ExpandHeight();
    }

    /// <summary>
    /// Enters this layout node's scope, making it the current active node in the GUI context.
    /// </summary>
    /// <returns>The current <see cref="Scope"/> after entering this node's scope.</returns>
    /// <remarks>
    /// Entering a node's scope affects how subsequent GUI operations are applied.
    /// Style settings and child node creation will be associated with this node's context.
    /// </remarks>
    public LayoutNodeScope Enter()
    {
        _gui.Enter(this);
        return _gui.CurrentNodeScope;
    }

    /// <summary>
    /// Exits this layout node's scope, returning to the previous scope in the GUI context.
    /// </summary>
    /// <returns>The current <see cref="Scope"/> after exiting this node's scope.</returns>
    /// <remarks>
    /// Exiting a node's scope typically returns control to the parent node's scope.
    /// This is important for proper scope management in nested layout structures.
    /// </remarks>
    public LayoutNodeScope Exit()
    {
        _gui.Exit();
        return _gui.CurrentNodeScope;
    }

    /// <summary>
    /// Disposes this layout node by exiting its scope.
    /// </summary>
    /// <remarks>
    /// This method implements the <see cref="IDisposable"/> interface and is typically
    /// called automatically when using the node in a using statement. It ensures proper
    /// cleanup of the node's scope in the GUI context.
    /// </remarks>
    public void Dispose()
    {
        Exit();
    }

    /// <summary>
    /// Creates a root layout node for the GUI system.
    /// </summary>
    /// <param name="gui">The GUI instance that will own the root node.</param>
    /// <param name="width">The width of the root node, typically the screen or window width.</param>
    /// <param name="height">The height of the root node, typically the screen or window height.</param>
    /// <returns>A new <see cref="LayoutNode"/> configured as a root node.</returns>
    /// <remarks>
    /// Root nodes serve as the top-level container for all other layout nodes in the GUI.
    /// They typically represent the full viewport or window area and have no parent node.
    /// </remarks>
    public static LayoutNode CreateRoot(Gui gui, float width, float height)
    {
        return new LayoutNode("0", gui, null, width, height);
    }

    /// <summary>
    /// Appends a new child node to the current layout node.
    /// </summary>
    /// <param name="sizeX">Node X size</param>
    /// <param name="sizeY">Node Y size</param>
    /// <param name="filePath">The source file path of the caller, automatically provided by the compiler.</param>
    /// <param name="lineNumber">The line number in the source file of the caller, automatically provided by the compiler.</param>
    /// <returns>A new instance of <see cref="LayoutNode"/> as a child of the current node.</returns>
    public LayoutNode AppendNode(
        UnitValue? sizeX = null,
        UnitValue? sizeY = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // var id = gui.NodeId(filePath, lineNumber);
        var node = new LayoutNode(null, _gui, this, sizeX, sizeY);
        return node;
    }

    /// <summary>
    /// Converts the current <see cref="LayoutNode"/> to its associated <see cref="LayoutNodeScope"/> instance.
    /// </summary>
    /// <returns>
    /// The <see cref="LayoutNodeScope"/> instance associated with this <see cref="LayoutNode"/>.
    /// </returns>
    public LayoutNodeScope ToScope()
    {
        return Scope;
    }
}
