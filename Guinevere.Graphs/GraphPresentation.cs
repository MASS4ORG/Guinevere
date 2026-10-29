namespace Guinevere.Graphs;

/// <summary>Zoom-dependent rendering detail; logical navigation and accessibility are unaffected.</summary>
public enum GraphLevelOfDetail
{
    /// <summary>Titles, ports with labels, badges, content and arrows.</summary>
    Full,
    /// <summary>Titles, port markers and badges without port labels or node content.</summary>
    Compact,
    /// <summary>Node silhouettes and thin links only; ports are neither drawn nor hit-tested.</summary>
    Outline
}

/// <summary>A port marker shape, chosen per port by the domain adapter.</summary>
public enum GraphPortShape
{
    /// <summary>A filled circle.</summary>
    Circle,
    /// <summary>A filled square.</summary>
    Square,
    /// <summary>A filled diamond.</summary>
    Diamond,
    /// <summary>A triangle pointing in the flow direction.</summary>
    Triangle
}
