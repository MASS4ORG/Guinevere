using MASS4.Attributes;

namespace Example_11_Excalibur_Attributes;

// [Hide] keeps a public member out of the form.
public sealed class HideDemo
{
    public string Visible { get; set; } = "You can see me";

    [Hide] public string Internal { get; set; } = "You cannot";
}
