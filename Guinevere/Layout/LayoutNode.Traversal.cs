namespace Guinevere;

/// <summary>
/// LayoutNode partial class containing node traversal and management operations
/// </summary>
public partial class LayoutNode
{
    #region Child Management

    /// <summary>
    /// Adds a child node to this layout node
    /// </summary>
    public LayoutNode AddChild(LayoutNode child)
    {
        if (ChildNodes.Contains(child)) return child;

        ChildNodes.Add(child);
        if (child.Style.IsAbsolute) _absoluteChildCount++;
        _flowChildrenCache = null;

        return child;
    }

    /// <summary>
    /// Removes a child node from this layout node
    /// </summary>
    public bool RemoveChild(LayoutNode child)
    {
        if (!ChildNodes.Remove(child)) return false;

        if (child.Style.IsAbsolute) _absoluteChildCount--;
        _flowChildrenCache = null;
        return true;
    }

    /// <summary>
    /// Removes a child node by its ID
    /// </summary>
    public bool RemoveChildById(string id)
    {
        return ChildNodes.Where(c => c.Id == id)
            .Take(1)
            .Aggregate(false, (_, child) => RemoveChild(child));
    }

    /// <summary>
    /// Clears all child nodes
    /// </summary>
    public void ClearChildren()
    {
        ChildNodes.Clear();
        _absoluteChildCount = 0;
        _flowChildrenCache = null;
    }

    /// <summary>
    /// Recursively clears this node and all its children
    /// </summary>
    public void ClearRoot()
    {
        ChildNodes.Clear();
        _absoluteChildCount = 0;
        _flowChildrenCache = null;
        DrawList = new DrawList();
    }

    #endregion

    #region Node Search

    /// <summary>
    /// Finds a child node by its ID (searches recursively)
    /// </summary>
    public LayoutNode? FindChildById(string id)
    {
        return FindChildByIdRecursive(id, ChildNodes);
    }

    static LayoutNode? FindChildByIdRecursive(string id, IEnumerable<LayoutNode> nodes)
    {
        return nodes.Select(child => child.Id == id ? child : FindChildByIdRecursive(id, child.ChildNodes))
            .FirstOrDefault(result => result != null);
    }

    #endregion

    #region Path Operations

    /// <summary>
    /// Gets the path from root to this node (useful for debugging)
    /// </summary>
    public string GetPath()
    {
        return GetAncestors()
            .Reverse()
            .Append(this)
            .Select(node => node.Id)
            .Aggregate((path, id) => $"{path}/{id}");
    }

    /// <summary>
    /// Gets all ancestor nodes from root to parent
    /// </summary>
    IEnumerable<LayoutNode> GetAncestors()
    {
        var current = Parent;
        while (current != null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    #endregion

    /// <summary>
    /// The first of it`s children
    /// </summary>
    public LayoutNode FirstChild => ChildNodes.First();

    /// <summary>
    /// The last of it`s children
    /// </summary>
    public LayoutNode LastChild => ChildNodes.Last();
}
