using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui : ILayoutNodeEnterExit
{
    /// <summary>Per-parent identity indexes built when the render pass diverges from the build pass.</summary>
    readonly Dictionary<LayoutNode, Dictionary<int, int>> _matchIndexes = new(ReferenceEqualityComparer.Instance);

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
    /// <param name="parentNode">An optional parent node used to scope the identifier. Defaults to the current node.</param>
    /// <returns>A formatted string representing the unique node identifier.</returns>
    public string NodeId(string filePath, int lineNumber, int extra = 0, LayoutNode? parentNode = null)
    {
        parentNode ??= CurrentNode;
        return IdentityName(_nodeIdentities.At(parentNode.Identity, _itemKey, filePath, lineNumber, extra,
            CurrentDataScope));
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
        UpdateInputBlocker();
    }

    LayoutNodeScope RegisterLayoutNodeScope(LayoutNode node)
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
        var identity = NodeIdentity(id, filePath, lineNumber, CurrentNode);
        if (id is null) CurrentNode.Pass2NodeCount++;
        SubmitIdentity(identity);
        var nodeExist = Pass == Pass.Pass1Build ? null : FindImmediateNode(identity);
        LayoutNode node;
        if (nodeExist is null) node = BuildNode(identity, width, height);
        else
        {
            node = nodeExist;
            node.Scope.Rebase();
        }

        // Reuse the command buffer whenever this immediate-mode node is rebuilt.
        node.Scope.ResetDataScope(CurrentDataScope);
        node.DrawList.Clear();
        node.Pass2NodeCount = 0;

        return node;
    }

    /// <summary>Creates a node from integer pixel dimensions.</summary>
    public LayoutNode Node(int width, int height, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        Node(width, (float)height, id, filePath, lineNumber);

    /// <summary>Creates a node using composable size expressions.</summary>
    public LayoutNode Node(UnitValue width, UnitValue height,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var identity = NodeIdentity(id, filePath, lineNumber, CurrentNode);
        if (id is null) CurrentNode.Pass2NodeCount++;
        SubmitIdentity(identity);
        var nodeExist = Pass == Pass.Pass1Build ? null : FindImmediateNode(identity);
        LayoutNode node;
        if (nodeExist is null)
        {
            node = BuildNode(identity, null, null);
            node.ApplyWidth(width);
            node.ApplyHeight(height);
        }
        else
        {
            node = nodeExist;
            node.Scope.Rebase();
        }

        node.Scope.ResetDataScope(CurrentDataScope);
        node.DrawList.Clear();
        node.Pass2NodeCount = 0;
        return node;
    }

    /// <summary>
    /// Adds a child for this frame under the current node, reusing the previous frame's node with the same identity
    /// (reset to a new node's state) or creating one.
    /// </summary>
    LayoutNode BuildNode(int identity, float? width, float? height)
    {
        var parent = CurrentNode;
        var node = parent.TakePreviousChild(identity);
        if (node is null) node = new LayoutNode(identity, this, parent, width, height);
        else node.ResetForBuild(width, height);
        parent.AppendNewChild(node);
        return node;
    }

    /// <summary>
    /// Finds the build-pass child the render pass is revisiting. Children come back in build order, so the parent's
    /// cursor finds each in constant time; a call that diverges resynchronizes through an identity index.
    /// </summary>
    LayoutNode? FindImmediateNode(int identity)
    {
        var parent = CurrentNode;
        var children = parent.ChildNodes;
        var cursor = parent.MatchCursor;
        if (cursor < children.Count && children[cursor].Identity == identity)
        {
            parent.MatchCursor = cursor + 1;
            return children[cursor];
        }
        return FindDivergedNode(parent, identity);
    }

    /// <summary>Looks a child up by identity, indexing the parent's children on its first mismatch this pass.</summary>
    LayoutNode? FindDivergedNode(LayoutNode parent, int identity)
    {
        var children = parent.ChildNodes;
        if (!_matchIndexes.TryGetValue(parent, out var index))
        {
            _matchIndexes.Add(parent, index = new Dictionary<int, int>(children.Count));
            for (var i = 0; i < children.Count; i++) index.TryAdd(children[i].Identity, i);
        }
        if (!index.TryGetValue(identity, out var position)) return null;
        parent.MatchCursor = position + 1;
        return children[position];
    }

    LayoutNode CreateRootNode(Rect rect)
    {
        var node = LayoutNode.CreateRoot(this, rect.W, rect.H);
        return RegisterLayoutNodeScope(node).Node;
    }
}
