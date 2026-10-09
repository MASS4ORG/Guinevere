using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [Tooltip] explains a member when its row is hovered.
public sealed class TooltipDemo
{
    [Tooltip("Seconds before an idle player is kicked.")] public int IdleTimeout { get; set; } = 300;
}
