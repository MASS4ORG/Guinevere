using System.Text;

namespace Guinevere;

/// <summary>Whether a column stays in view while the rest of the table scrolls sideways.</summary>
public enum TablePin
{
    /// <summary>Scrolls with the other columns.</summary>
    None,

    /// <summary>Stays at the left edge.</summary>
    Left,

    /// <summary>Stays at the right edge.</summary>
    Right
}

/// <summary>
/// What layout and the header need of any column: its title, width, alignment and whether it can be sorted.
/// The column instance is the column's identity everywhere a table refers to one, including sort keys; the
/// string <see cref="Id"/> exists only so persisted state can find the column again. Build columns with the
/// factories on <see cref="TableColumn{TRow}"/>.
/// </summary>
public abstract class TableColumn
{
    internal static readonly TableLength DefaultWidth = TableLength.Star();

    internal const float DefaultMinWidth = 48f;

    string? _id;
    string? _positionalId;

    /// <summary>The hierarchy is closed: the table needs to call the typed members of its own subclasses.</summary>
    private protected TableColumn(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        Title = title;
    }

    /// <summary>
    /// The header text, which may be empty. It is what the header shows when <see cref="Header"/> is not set,
    /// what an Auto width is measured from, and what the default <see cref="Id"/> comes from.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// What the header cell draws instead of the title text: an icon, a localized text, another widget or
    /// anything implementing <see cref="ITableHeaderContent"/>. Null draws <see cref="Title"/>.
    /// </summary>
    public ITableHeaderContent? Header { get; init; }

    /// <summary>
    /// The key persisted column state uses to find this column again; unique in a table. Left out, it is the
    /// title in lower case with everything but letters and digits removed ("File Name" is <c>filename</c>);
    /// a title with none of those (an empty one, or only an icon) falls back to <c>column</c> and the
    /// column's position in the <see cref="TableColumns{TRow}"/> that holds it, counting from one.
    /// Set it explicitly on every column whose state is persisted across sessions, because retitling,
    /// translating or reordering columns changes the derived key and orphans the stored state, and on columns
    /// built in a loop or sharing a title.
    /// </summary>
    public string Id
    {
        get => _id ?? DerivedId;
        init => _id = value;
    }

    string DerivedId
    {
        get
        {
            var slug = DeriveId(Title);
            return slug.Length > 0 ? slug : _positionalId ?? "";
        }
    }

    /// <summary>Called by the collection that holds the column, so a column with no title text still has an id.</summary>
    internal void AssignPositionalId(int position) => _positionalId = "column" + (position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The id the factories pass on, where null means "derive it from the title".</summary>
    internal string? ExplicitId
    {
        get => _id;
        init => _id = value;
    }

    /// <summary>How the width is decided; an equal share of the free width by default.</summary>
    public TableLength Width { get; init; } = DefaultWidth;

    /// <summary>The narrowest the column may be, in pixels.</summary>
    public float MinWidth { get; init; } = DefaultMinWidth;

    /// <summary>The widest the column may be, in pixels.</summary>
    public float MaxWidth { get; init; } = float.PositiveInfinity;

    /// <summary>Where the header and cell content sit inside the column.</summary>
    public float AlignContentX { get; init; }

    /// <summary>Where the header and cell content sit inside the column.</summary>
    public float AlignmentY { get; protected init; } = .5f;

    /// <summary>Whether the column stays at an edge while the rest scrolls sideways.</summary>
    public TablePin Pin { get; init; } = TablePin.None;

    /// <summary>Whether the column is shown before the user has chosen otherwise.</summary>
    public bool InitiallyVisible { get; init; } = true;

    /// <summary>
    /// Whether the column can be ordered by: its value type is comparable or a comparer was given when it was
    /// built. A column that cannot be sorted gets no sort marker and cannot be a sort key.
    /// </summary>
    public bool Sortable { get; private protected set; }

    /// <summary>The title in lower case with everything but letters and digits removed.</summary>
    internal static string DeriveId(string title)
    {
        var slug = new StringBuilder(title.Length);
        foreach (var character in title)
        {
            if (char.IsLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
            }
        }

        return slug.ToString();
    }
}
