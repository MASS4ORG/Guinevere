namespace Guinevere;

/// <summary>Built-in interaction states available to style selectors.</summary>
[Flags]
public enum StyleState
{
    /// <summary>No interaction state.</summary>
    None = 0,
    /// <summary>The pointer is over the element.</summary>
    Hover = 1,
    /// <summary>The element is pressed.</summary>
    Active = 2,
    /// <summary>The element has keyboard focus.</summary>
    Focus = 4,
    /// <summary>The element is disabled.</summary>
    Disabled = 8,
}

/// <summary>
/// What a selector is matched against in the current frame. The core only records it per node; the
/// <c>Guinevere.Styling</c> package parses sheets and resolves styles for it.
/// </summary>
public readonly record struct StyleTarget(string? Type, string? Id, IReadOnlyList<string> Classes,
    StyleState State = StyleState.None, IReadOnlyList<string>? Modifiers = null,
    IReadOnlyList<StyleTarget>? Ancestors = null);
