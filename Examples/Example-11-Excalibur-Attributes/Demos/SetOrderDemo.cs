using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [SetOrder] sorts members: lower first, ties keep declaration order (default 0).
/// <summary>Demonstrates ordering members in the generated form.</summary>
public sealed class SetOrderDemo
{
    /// <summary>Member at the first default order position.</summary>
    public string Second { get; set; } = "declared first, order 0";

    /// <summary>Member placed after the default ordered fields.</summary>
    [SetOrder(10)] public string Last { get; set; } = "order 10";

    /// <summary>Member retaining its declaration order among defaults.</summary>
    public string Third { get; set; } = "declared third, order 0";

    /// <summary>Member placed before the default ordered fields.</summary>
    [SetOrder(-1)] public string First { get; set; } = "order -1";
}
