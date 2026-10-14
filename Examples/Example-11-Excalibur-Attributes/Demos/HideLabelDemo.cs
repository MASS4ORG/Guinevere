using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [HideLabel] lets the editor span the whole row.
public sealed class HideLabelDemo
{
    [HideLabel] public string Motto { get; set; } = "A label-free row uses the full width";

    public string WithLabel { get; set; } = "For comparison";
}
