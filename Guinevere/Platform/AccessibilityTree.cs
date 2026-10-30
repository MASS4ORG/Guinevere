namespace Guinevere;

/// <summary>A portable logical accessible, including elements without a rendered layout node.</summary>
/// <param name="Id">Stable identity within the host.</param>
/// <param name="ParentId">Logical parent, or null for a window child.</param>
/// <param name="Role">Semantic role understood by the native adapter.</param>
/// <param name="Name">Human-readable accessible name.</param>
/// <param name="Description">Type, state and validation details.</param>
/// <param name="Bounds">Bounds in window coordinates.</param>
/// <param name="State">Interactive state.</param>
/// <param name="Virtualized">Whether geometry is currently outside the rendered viewport.</param>
/// <param name="Actions">Named operations offered by this element.</param>
/// <param name="ScopeId">Action recipient, such as a graph canvas.</param>
/// <param name="Key">Opaque recipient-local element key.</param>
/// <param name="SourceId">Source of a relationship such as a graph edge.</param>
/// <param name="TargetId">Target of a relationship such as a graph edge.</param>
/// <param name="Expanded">Expanded state of a container, when applicable.</param>
public sealed record AccessibilityElement(string Id, string? ParentId, string Role, string Name,
    string Description, Rect Bounds, ControlVisualState State, bool Virtualized = false,
    IReadOnlyList<string>? Actions = null, string? ScopeId = null, string? Key = null,
    string? SourceId = null, string? TargetId = null, bool? Expanded = null);

/// <summary>An operation queued by an assistive technology for consumption on the UI thread.</summary>
public sealed record AccessibilityAction(string Name, string? Key = null);

/// <summary>Receives richer logical metadata without depending on a domain-specific GUI package.</summary>
public interface IAccessibilityTreeSink : IPlatformCapability
{
    /// <summary>Replaces the current frame's metadata for an element.</summary>
    void PublishElement(AccessibilityElement element);

    /// <summary>Announces a completed operation or observed state change in the named scope.</summary>
    void Announce(string scopeId, string message, string? elementId = null);
}

/// <summary>Routes native actions to a domain's existing UI-thread command queue.</summary>
public interface IAccessibilityActionSource : IPlatformCapability
{
    /// <summary>Removes one request for the named scope, preserving other scopes' requests.</summary>
    bool TryDequeue(string scopeId, out AccessibilityAction action);
}
