namespace Guinevere;

/// <summary>
/// Where a panel lands when it is dropped on a dock target.
/// </summary>
public enum DockZone
{
    /// <summary>Join the target's tab group.</summary>
    Center,

    /// <summary>Split the target and take the left half.</summary>
    Left,

    /// <summary>Split the target and take the right half.</summary>
    Right,

    /// <summary>Split the target and take the top half.</summary>
    Top,

    /// <summary>Split the target and take the bottom half.</summary>
    Bottom
}
