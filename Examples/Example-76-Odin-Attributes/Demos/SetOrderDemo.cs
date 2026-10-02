using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [SetOrder] sorts members: lower first, ties keep declaration order (default 0).
public sealed class SetOrderDemo
{
    public string Second { get; set; } = "declared first, order 0";

    [SetOrder(10)] public string Last { get; set; } = "order 10";

    public string Third { get; set; } = "declared third, order 0";

    [SetOrder(-1)] public string First { get; set; } = "order -1";
}
