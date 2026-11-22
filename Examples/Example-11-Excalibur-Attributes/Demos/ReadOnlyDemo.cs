using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [ReadOnly] shows a value without letting it change; readonly fields are read-only already.
/// <summary>Shows read-only properties and fields in the attribute editor.</summary>
public sealed class ReadOnlyDemo
{
    /// <summary>Seed value displayed without an editor.</summary>
    [ReadOnly] public int Seed { get; set; } = 1234;

    /// <summary>Version string displayed as an immutable field.</summary>
    public readonly string Version = "1.0.0";

    /// <summary>Credits displayed without allowing edits.</summary>
    [ReadOnly] public List<string> Credits { get; set; } = ["Guinevere", "Autoformers"];
}
