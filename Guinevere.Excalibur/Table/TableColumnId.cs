namespace Guinevere;

/// <summary>
/// The key of one table column, used to key persisted column state, sort requests and the source's
/// <c>GetValue</c>. It is a distinct type on purpose, with no conversion to or from <see cref="string"/>,
/// so a title or a row value cannot be passed where a column key goes.
/// </summary>
/// <param name="Value">
/// The key text. Persisted state and sort keys store this string unchanged, so a stored key stays valid
/// for as long as the column keeps the same value.
/// </param>
public readonly record struct TableColumnId(string Value)
{
    /// <summary>The key's text.</summary>
    public override string ToString() => Value;

    /// <summary>The key a column gets when it is given none: the declaring file's name and line.</summary>
    internal static TableColumnId FromSite(string filePath, int lineNumber) =>
        new($"{Path.GetFileName(filePath)}:{lineNumber}");
}
