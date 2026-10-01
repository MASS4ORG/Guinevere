namespace Guinevere.Forms;

/// <summary>
/// A button the inspector draws to run an action on the inspected object — the editor half of a
/// <c>[Button]</c> method, the way Odin lets a tool run straight from a scriptable object's members.
/// </summary>
/// <param name="Label">The text the button shows.</param>
/// <param name="Invoke">Runs the annotated method on the form's target.</param>
/// <param name="CanInvoke">Whether the action can run in the current context.</param>
public sealed record Button(string Label, Action Invoke, Func<bool>? CanInvoke = null)
{
    /// <summary>Whether the action is currently available.</summary>
    public bool IsEnabled => CanInvoke?.Invoke() ?? true;
}
