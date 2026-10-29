namespace Guinevere.Graphs;

/// <summary>Logical operations an accessibility adapter can request without pointer coordinates.</summary>
public enum GraphActionKind
{
    /// <summary>Focuses and reveals an element, or focuses the canvas when no key is supplied.</summary>
    Focus,
    /// <summary>Replaces selection.</summary>
    Select,
    /// <summary>Toggles selection membership.</summary>
    ToggleSelection,
    /// <summary>Enters node ports or starts/completes a connection.</summary>
    Activate,
    /// <summary>Connects the two named ports through the attached editor.</summary>
    Connect,
    /// <summary>Deletes the addressed selection or element.</summary>
    Delete,
    /// <summary>Cancels an interaction or returns to its owner.</summary>
    Cancel,
    /// <summary>Opens command search.</summary>
    SearchCommands,
    /// <summary>Opens registered node creation.</summary>
    CreateNode,
    /// <summary>Selects all nodes and links.</summary>
    SelectAll,
    /// <summary>Undoes the latest editor command.</summary>
    Undo,
    /// <summary>Redoes the latest undone command.</summary>
    Redo,
    /// <summary>Copies the selection to the host clipboard.</summary>
    Copy,
    /// <summary>Copies and deletes the selection.</summary>
    Cut,
    /// <summary>Pastes a subgraph from the host clipboard.</summary>
    Paste
}

/// <summary>A logical action addressed by catalog keys from graph semantics, scoped by the receiving canvas.</summary>
public sealed record GraphAction(GraphActionKind Kind, string? Key = null, string? TargetKey = null);

/// <summary>A platform action queue; requests are consumed only by the canvas whose stable ID is supplied.</summary>
public interface IGraphActionSource : IPlatformCapability
{
    /// <summary>Removes one queued request for this canvas; other canvases' requests must remain queued.</summary>
    bool TryDequeue(string graphId, out GraphAction action);
}
