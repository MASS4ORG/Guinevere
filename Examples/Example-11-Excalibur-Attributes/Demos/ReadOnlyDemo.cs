using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [ReadOnly] shows a value without letting it change; readonly fields are read-only already.
public sealed class ReadOnlyDemo
{
    [ReadOnly] public int Seed { get; set; } = 1234;

    public readonly string Version = "1.0.0";

    [ReadOnly] public List<string> Credits { get; set; } = ["Guinevere", "Autoformers"];
}
