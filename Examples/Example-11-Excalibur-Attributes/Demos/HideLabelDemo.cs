using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [HideLabel] lets the editor span the whole row.
/// <summary>Compares a full-width field with a labeled field.</summary>
public sealed class HideLabelDemo
{
    /// <summary>Text field displayed without its property label.</summary>
    [HideLabel] public string Motto { get; set; } = "A label-free row uses the full width";

    /// <summary>Text field displayed with its property label.</summary>
    public string WithLabel { get; set; } = "For comparison";
}
