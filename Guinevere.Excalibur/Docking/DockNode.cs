namespace Guinevere;

/// <summary>
/// A node in a dock tree: either a <see cref="DockLeaf"/> holding panels, or a
/// <see cref="DockSplit"/> dividing the space between two children.
/// </summary>
public abstract class DockNode
{
    /// <summary>
    /// Enumerates this node and everything below it, parents before children.
    /// </summary>
    public IEnumerable<DockNode> Descend()
    {
        yield return this;

        if (this is not DockSplit split) yield break;

        foreach (var node in split.First.Descend()) yield return node;
        foreach (var node in split.Second.Descend()) yield return node;
    }

    /// <summary>
    /// Enumerates the leaves below this node, left to right and top to bottom.
    /// </summary>
    public IEnumerable<DockLeaf> Leaves() => Descend().OfType<DockLeaf>();
}
