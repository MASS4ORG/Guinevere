using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [Tooltip] explains a member when its row is hovered.
/// <summary>Demonstrates help text attached to a property editor.</summary>
public sealed class TooltipDemo
{
    /// <summary>Seconds an idle player may remain connected.</summary>
    [Tooltip("Seconds before an idle player is kicked.")] public int IdleTimeout { get; set; } = 300;
}
