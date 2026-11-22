using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [Show] reveals private members; a shown get-only property is read-only.
/// <summary>Demonstrates exposing selected private and read-only members.</summary>
public sealed class ShowDemo
{
    [Show] readonly int _secretLevel = 7;

    /// <summary>Read-only value derived from the private seed.</summary>
    [Show] public int Doubled => _secretLevel * 2;

    /// <summary>Property omitted from the generated form.</summary>
    public int Hidden => 42;

    /// <summary>Build identifier displayed as an immutable field.</summary>
    public readonly string BuildId = "2026.10";
}
