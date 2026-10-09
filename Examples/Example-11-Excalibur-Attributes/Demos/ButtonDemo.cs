using MASS4.Attributes;

namespace Example_11_Excalibur_Attributes;

// [Button] turns a public parameterless method into a button under the members.
public sealed class ButtonDemo
{
    [ReadOnly]
    public int Clicks { get; set; }

    [Button]
    public void AddClick() => Clicks++;

    [Button]
    public void ResetClicks() => Clicks = 0;
}
