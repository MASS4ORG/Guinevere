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

    // The constructor arguments need no guard: a split being constructed cannot yet be passed to itself.
    private DockNode _first = first;
    private DockNode _second = second;

    /// <summary>
    /// The left or top child.
    /// </summary>
    public DockNode First
    {
        get => _first;
        set => _first = Guard(value);
    }

    /// <summary>
    /// The right or bottom child.
    /// </summary>
    public DockNode Second
    {
        get => _second;
        set => _second = Guard(value);
    }

    /// <summary>
    /// A split that holds itself makes every traversal recurse forever, and the stack overflow that
    /// follows points at the walk rather than at whatever built the cycle. Refuse it at the assignment.
    /// </summary>
    private DockNode Guard(DockNode child)
    {
        if (ReferenceEquals(child, this))
            throw new ArgumentException("A DockSplit cannot be its own child.", nameof(child));

        return child;
    }

    /// <summary>
    /// The share of the region <see cref="First"/> takes, 0..1. The splitter writes to this.
    /// </summary>
    public float Fraction { get; set; } = fraction;
}
