namespace Guinevere;

/// <summary>A requested inclusion or removal of one option.</summary>
/// <param name="Item">The option affected by the edit.</param>
/// <param name="Selected">Whether the option should be included.</param>
public readonly record struct SelectionChange<T>(T Item, bool Selected);

/// <summary>A controlled selection and the individual edits delivered in the build pass.</summary>
/// <param name="Selected">Distinct values in option order, followed by values outside the options in their input order.</param>
/// <param name="Changes">Edits to apply to other owners when editing a mixed selection.</param>
public sealed record MultiDropdownResult<T>(IReadOnlyList<T> Selected, IReadOnlyList<SelectionChange<T>> Changes)
{
    /// <summary>Whether this call delivers selection edits; false in the render pass.</summary>
    public bool Changed => Changes.Count > 0;
}
