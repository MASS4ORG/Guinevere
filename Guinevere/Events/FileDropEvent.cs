namespace Guinevere;

/// <summary>Desktop files dropped onto a node, routed through its capture and bubble listeners.</summary>
public sealed class FileDropEvent : GuiEvent
{
    /// <summary>The drop position in window coordinates.</summary>
    public Vector2 Position { get; internal init; }

    /// <summary>The operating-system paths, copied before the event was queued.</summary>
    public IReadOnlyList<string> Paths { get; internal init; } = [];
}
