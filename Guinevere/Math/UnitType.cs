#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Guinevere;

/// <summary>
/// Represents a type or mode of a unit, used to define how a particular value
/// should be interpreted or scaled. UnitType allows for different measurement
/// systems and interpretations, such as absolute pixel values, relative ratios,
/// or percentages.
/// </summary>
public enum UnitType
{
    Auto,
    Expand,
    Ratio,
    Percentage,
    Pixels,
    FitLargest
}
