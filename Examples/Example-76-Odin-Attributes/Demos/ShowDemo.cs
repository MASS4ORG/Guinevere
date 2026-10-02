using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [Show] reveals private members; a shown get-only property is read-only.
public sealed class ShowDemo
{
    [Show] int _secretLevel = 7;

    [Show] public int Doubled => _secretLevel * 2;

    public int Hidden => 42;

    public readonly string BuildId = "2026.10";
}
