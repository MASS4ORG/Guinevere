using MASS4.Attributes;

namespace Example_11_Excalibur_Attributes;

// [Tooltip] explains a member when its row is hovered.
public sealed class TooltipDemo
{
    [Tooltip("Seconds before an idle player is kicked.")] public int IdleTimeout { get; set; } = 300;
}
