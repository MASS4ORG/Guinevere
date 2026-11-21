namespace Guinevere;

/// <summary>
/// Events a widget reports on the node it returns, for the frame that built it. Widgets raise them in the render pass,
/// so they read <c>false</c> in the build pass.
/// </summary>
[Flags]
public enum NodeSignals
{
    /// <summary>No event.</summary>
    None = 0,

    /// <summary>Clicked, or activated from the keyboard while focused.</summary>
    Clicked = 1,

    /// <summary>The widget changed the value it edits.</summary>
    Changed = 2,

    /// <summary>The value was committed, such as Enter in a text field.</summary>
    Submitted = 4,
}
