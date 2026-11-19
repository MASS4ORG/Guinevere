namespace Guinevere;

/// <summary>
/// The node a widget (control) returns. It is a <see cref="LayoutNode"/>, so layout and box calls chain on it, and it
/// converts to <c>bool</c> as "the widget reported an event this frame" (clicked, changed or submitted), so
/// <c>if (gui.Button("OK"))</c> and <c>if (gui.Checkbox(ref on, "Dark"))</c> read like any immediate-mode GUI. Fluent
/// calls return the base <see cref="LayoutNode"/>, so after a chain read <see cref="LayoutNode.Clicked"/> or
/// <see cref="LayoutNode.Changed"/> explicitly; plain nodes never convert to <c>bool</c>.
/// </summary>
public sealed class ControlNode : LayoutNode
{
    internal ControlNode(int identity, Gui gui, LayoutNode? parent, float? width, float? height)
        : base(identity, gui, parent, width, height)
    {
    }

    /// <summary>Whether the widget reported any event this frame; <c>false</c> for <c>null</c>.</summary>
    /// <param name="node">The widget's node.</param>
    public static implicit operator bool(ControlNode? node) => node is not null && node.Signals != NodeSignals.None;
}
