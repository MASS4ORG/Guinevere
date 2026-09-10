namespace Guinevere;

/// <summary>
/// Selects the coordinate space an absolutely-positioned node is placed in.
/// </summary>
public enum AbsoluteOrigin
{
    /// <summary>
    /// The position is an offset from the top-left of the parent node's content box (its
    /// <see cref="LayoutNode.InnerRect"/>), the way CSS <c>position: absolute</c> reads.
    /// </summary>
    Parent,

    /// <summary>
    /// The position is in screen space, independent of where the node sits in the tree.
    /// Overlays that compute a position from the cursor or from another node's rect use this.
    /// </summary>
    Screen
}
