namespace Guinevere;

/// <summary>
/// Divides a region between two child nodes along one axis.
/// </summary>
/// <param name="axis">Horizontal places the children side by side; vertical stacks them.</param>
/// <param name="first">The left or top child.</param>
/// <param name="second">The right or bottom child.</param>
/// <param name="fraction">The share of the region <paramref name="first"/> takes, 0..1.</param>
public sealed class DockSplit(Axis axis, DockNode first, DockNode second, float fraction = 0.5f) : DockNode
{
    /// <summary>
    /// Horizontal places the children side by side; vertical stacks them.
    /// </summary>
    public Axis Axis { get; set; } = axis;

    /// <summary>
    /// The left or top child.
    /// </summary>
    public DockNode First { get; set; } = first;

    /// <summary>
    /// The right or bottom child.
    /// </summary>
    public DockNode Second { get; set; } = second;

    /// <summary>
    /// The share of the region <see cref="First"/> takes, 0..1. The splitter writes to this.
    /// </summary>
    public float Fraction { get; set; } = fraction;
}
