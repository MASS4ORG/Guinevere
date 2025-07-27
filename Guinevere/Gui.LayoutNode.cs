using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui : ILayoutNodeEnterExit
{
    /// <summary>
    /// Retrieves the currently active <see cref="LayoutNode"/> within the layout context.
    /// </summary>
    /// <remarks>
    /// The <see cref="CurrentNode"/> property provides access to the active layout node
    /// associated with the current <see cref="LayoutNodeScope"/>. This is used for rendering,
    /// layout adjustments, and hierarchical relationships during layout processing.
    /// </remarks>
    public LayoutNode CurrentNode => CurrentNodeScope.Node;

    /// <summary>
    /// Provides the current <see cref="LayoutNodeScope"/> within the context of the layout system.
    /// </summary>
    /// <remarks>
    /// The <see cref="CurrentNodeScope"/> property retrieves the active scope from the internal stack
    /// used to manage the hierarchical context of layout nodes during rendering and layout processing.
    /// This allows for the tracking and manipulation of layout-specific properties (e.g., text styling,
    /// z-order) within the current node's scope.
    /// </remarks>
    public LayoutNodeScope CurrentNodeScope => LayoutNodeScopeStack.Peek();

    /// <summary>
    /// Represents a stack of <see cref="LayoutNodeScope"/> objects used to manage the hierarchical structure
    /// and context of layout nodes during GUI rendering and layout calculations.
    /// </summary>
    /// <remarks>
    /// The <see cref="LayoutNodeScopeStack"/> is a core part of the layout management system, allowing for
    /// the tracking of nested scopes as layout nodes are entered and exited within the rendering process.
    /// It ensures proper handling of layout hierarchies and context-specific properties such as styling,
    /// positioning, and z-order.
    /// </remarks>
    public Stack<LayoutNodeScope> LayoutNodeScopeStack { get; } = new();

    /// <summary>
    /// Represents the root layout node in the hierarchy of the graphical user interface (GUI).
    /// This node serves as the container and entry point for all other layout nodes.
    /// </summary>
    /// <remarks>
    /// The <see cref="RootNode"/> property is the starting point for layout calculations
    /// and traversal operations, influencing how child nodes are structured and rendered.
    /// It is initialized during the beginning of a frame and supports layout recalculations.
    /// </remarks>
    public LayoutNode? RootNode { get; private set; }

    /// <summary>
    /// Represents the current operational stage of the graphical user interface (GUI) rendering process.
    /// Determines whether the GUI is in the initial layout-building phase or the final rendering phase.
    /// </summary>
    /// <remarks>
    /// The <see cref="Pass"/> property influences behavior in various methods and controls how layout nodes are processed.
    /// </remarks>
    public Pass Pass { get; private set; }

    /// <summary>
    /// Generates a unique node identifier based on the provided file path, line number, and optional extra parameter.
    /// </summary>
    /// <param name="filePath">The source file path where the node is being defined.</param>
    /// <param name="lineNumber">The line number in the source file where the node is being defined.</param>
    /// <param name="extra">An optional integer to append additional uniqueness to the identifier. Defaults to 0.</param>
    /// <returns>A formatted string representing the unique node identifier.</returns>
    public string NodeId(string filePath, int lineNumber, int extra = 0, LayoutNode? parentNode = null)
    {
        var idLocal = $"{filePath}:{lineNumber} {extra}";
        parentNode ??= CurrentNode;
        var idGlobal = $"{parentNode?.Id}{idLocal}";
        return idGlobal;
    }

    /// <summary>
    /// Enters a given layout node context, registers it in the scope stack, and returns the associated layout node scope.
    /// </summary>
    /// <param name="node">The layout node to enter and register in the context.</param>
    /// <returns>The scope associated with the entered layout node.</returns>
    public LayoutNodeScope Enter(LayoutNode node)
    {
        return RegisterLayoutNodeScope(node);
    }

    /// <summary>
    /// Exits the current layout node scope and returns the associated layout node.
    /// </summary>
    /// <returns>The layout node associated with the exited scope.</returns>
    public LayoutNode Exit()
    {
        if (LayoutNodeScopeStack.Count == 1) return CurrentNodeScope.Node;

        var scope = LayoutNodeScopeStack.Pop();

        return scope.Node;
    }

    /// <summary>
    /// Performs layout calculations on the root node of the GUI tree.
    /// This ensures that all nodes have their layout properties properly computed based on the hierarchy and cascading style rules.
    /// </summary>
    public void CalculateLayout()
    {
        RootNode!.CalculateLayout();
    }

    private LayoutNodeScope RegisterLayoutNodeScope(LayoutNode node)
    {
        LayoutNodeScopeStack.Push(node.Scope);
        return node.Scope;
    }

    /// <summary>
    /// Creates a node with a specified size
    /// </summary>
    public LayoutNode Node(float width = -1, float height = -1,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        id ??= NodeId(filePath, lineNumber, CurrentNode.Pass2NodeCount++);
        LayoutNode node;
        var nodeExist = CurrentNode.Children.FirstOrDefault(child => child.Id == id);
        if (Pass == Pass.Pass1Build || nodeExist is null)
        {
            node = new LayoutNode(id, this, CurrentNode, width, height);
            CurrentNode.AddChild(node);
        }
        else
        {
            node = nodeExist;
        }

        // Only reset DrawList during Pass1Build phase to prevent clearing render commands
        node.DrawList = new DrawList();
        node.Pass2NodeCount = 0;

        return node;
    }

    private LayoutNode CreateRootNode(Rect rect)
    {
        var node = LayoutNode.CreateRoot(this, rect.W, rect.H);
        return RegisterLayoutNodeScope(node).Node;
    }
}
