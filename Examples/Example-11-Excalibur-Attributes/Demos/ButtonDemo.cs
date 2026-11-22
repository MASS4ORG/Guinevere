using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [Button] turns a public parameterless method into a button under the members.
/// <summary>Demonstrates methods exposed as form buttons.</summary>
public sealed class ButtonDemo
{
    /// <summary>Number of times an example button was activated.</summary>
    [ReadOnly]
    public int Clicks { get; set; }

    /// <summary>Increments the click count.</summary>
    [Button]
    public void AddClick() => Clicks++;

    /// <summary>Resets the click count.</summary>
    [Button]
    public void ResetClicks() => Clicks = 0;
}
