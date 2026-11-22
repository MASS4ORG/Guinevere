using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [Hide] keeps a public member out of the form.
/// <summary>Demonstrates excluding a property from the generated form.</summary>
public sealed class HideDemo
{
    /// <summary>Property visible in the generated form.</summary>
    public string Visible { get; set; } = "You can see me";

    /// <summary>Property excluded from the generated form.</summary>
    [Hide] public string Internal { get; set; } = "You cannot";
}
