namespace Guinevere;

/// <summary>
/// One item of a <c>ButtonGroup</c>: any <see cref="Content"/> plus an enabled flag and an optional stable id. A
/// string, an icon or a <see cref="Content"/> converts implicitly, so a plain list of labels is a valid item list.
/// </summary>
/// <param name="Content">What the item shows; an icon should carry a label (<see cref="Icon.Labeled"/>).</param>
/// <param name="Enabled">When false the item matches <c>:disabled</c>, is skipped by arrow keys and never activates.</param>
/// <param name="Id">
/// Optional id for <c>button-group-item#id</c> rules and an identity that survives reordering; the item's index is the
/// default.
/// </param>
public readonly record struct ButtonGroupItem(Content Content, bool Enabled = true, string? Id = null)
{
    /// <summary>An enabled item showing <paramref name="text"/>.</summary>
    /// <param name="text">The item's text.</param>
    public static implicit operator ButtonGroupItem(string? text) => new(text);

    /// <summary>An enabled item showing <paramref name="icon"/>.</summary>
    /// <param name="icon">The item's icon.</param>
    public static implicit operator ButtonGroupItem(Icon? icon) => new(icon);

    /// <summary>An enabled item showing <paramref name="content"/>.</summary>
    /// <param name="content">The item's content.</param>
    public static implicit operator ButtonGroupItem(Content content) => new(content);
}
