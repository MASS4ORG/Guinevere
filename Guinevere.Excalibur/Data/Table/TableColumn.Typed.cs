namespace Guinevere;

/// <summary>
/// A column over rows of one type. The table draws and orders through this type, so it never has to know
/// the type of the column's value. Create columns with <see cref="Of{TValue}"/> and <see cref="Row"/>.
/// </summary>
/// <typeparam name="TRow">The type of a row, as the source's indexer returns it.</typeparam>
public abstract class TableColumn<TRow> : TableColumn
{
    private protected TableColumn(string title)
        : base(title)
    {
    }

    /// <summary>Builds the content of this column's cell for one row, in the cell node the table made.</summary>
    internal abstract void RenderCell(Gui gui, TRow item, in TableCellFrame<TRow> frame);

    /// <summary>Orders two rows by this column's value.</summary>
    internal abstract int Compare(TRow left, TRow right);

    /// <summary>The text of this column's value in a row, for an assistive technology to read.</summary>
    internal abstract string Text(TRow item);

    /// <summary>A column whose cells show a value read from the row.</summary>
    /// <typeparam name="TValue">The value's type; its default comparison decides whether the column sorts.</typeparam>
    /// <param name="title">The header text, which may be empty when <paramref name="header"/> draws the header.</param>
    /// <param name="value">Reads the cell's value from the row.</param>
    /// <param name="id">Key for persisted column state; defaults to the title slug. See <see cref="TableColumn.Id"/>.</param>
    /// <param name="width">How the width is decided; an equal share of the free width by default.</param>
    /// <param name="minWidth">The narrowest the column may be, in pixels.</param>
    /// <param name="maxWidth">The widest the column may be, in pixels.</param>
    /// <param name="alignContentX">Where the header and cell content sit across the column: 0 leading, 1 trailing.</param>
    /// <param name="alignContentY">Where the header and cell content sit down the row: 0 top, 1 bottom.</param>
    /// <param name="pin">Which edge the column is held at while the rest scrolls sideways.</param>
    /// <param name="visible">Whether the column is shown before the user has chosen otherwise.</param>
    /// <param name="comparer">Orders the values; makes a column sortable whose type has no natural order.</param>
    /// <param name="renderer">Draws the cell; text of the value when null.</param>
    /// <param name="header">Drawn instead of the title text: an icon, localized text, other widgets.</param>
    /// <param name="resizable">Whether the user can drag the column's edge or use the keyboard to change its width.</param>
    public static TableColumn<TRow, TValue> Of<TValue>(string title, Func<TRow, TValue> value, string? id = null,
        TableLength? width = null, float? minWidth = null, float? maxWidth = null,
        float alignContentX = 0f, float alignContentY = 0.5f, TablePin pin = TablePin.None, bool visible = true,
        IComparer<TValue>? comparer = null, ITableCellRenderer<TRow, TValue>? renderer = null,
        Content header = default, bool resizable = true) =>
        new(title, value, comparer, renderer)
        {
            ExplicitId = id,
            Header = header,
            Width = width ?? DefaultWidth,
            MinWidth = minWidth ?? DefaultMinWidth,
            MaxWidth = maxWidth ?? float.PositiveInfinity,
            AlignContentX = alignContentX,
            AlignContentY = alignContentY,
            Pin = pin,
            InitiallyVisible = visible,
            Resizable = resizable
        };

    /// <summary>
    /// A column whose value is the row itself, for cells a renderer draws from several fields such as an
    /// actions column. Without a renderer it shows the row's text.
    /// </summary>
    /// <param name="title">The header text, which may be empty when <paramref name="header"/> draws the header.</param>
    /// <param name="id">Key for persisted column state; defaults to the title slug. See <see cref="TableColumn.Id"/>.</param>
    /// <param name="width">How the width is decided; an equal share of the free width by default.</param>
    /// <param name="minWidth">The narrowest the column may be, in pixels.</param>
    /// <param name="maxWidth">The widest the column may be, in pixels.</param>
    /// <param name="alignContentX">Where the header and cell content sit across the column: 0 leading, 1 trailing.</param>
    /// <param name="alignContentY">Where the header and cell content sit down the row: 0 top, 1 bottom.</param>
    /// <param name="pin">Which edge the column is held at while the rest scrolls sideways.</param>
    /// <param name="visible">Whether the column is shown before the user has chosen otherwise.</param>
    /// <param name="comparer">Orders the rows; makes the column sortable when the row type has no natural order.</param>
    /// <param name="renderer">Draws the cell from the row; text of the row when null.</param>
    /// <param name="header">Drawn instead of the title text: an icon, localized text, other widgets.</param>
    /// <param name="resizable">Whether the user can drag the column's edge or use the keyboard to change its width.</param>
    public static TableColumn<TRow, TRow> Row(string title, string? id = null, TableLength? width = null,
        float? minWidth = null, float? maxWidth = null, float alignContentX = 0f, float alignContentY = 0.5f,
        TablePin pin = TablePin.None, bool visible = true, IComparer<TRow>? comparer = null,
        ITableCellRenderer<TRow, TRow>? renderer = null, Content header = default, bool resizable = true) =>
        Of(title, static row => row, id, width, minWidth, maxWidth, alignContentX, alignContentY, pin, visible,
            comparer, renderer, header, resizable);

    /// <summary>
    /// A column of expanders: a chevron on every row whose details exist (see
    /// <see cref="TableOptions{TRow}.RowDetails"/>) that expands and collapses the row. It cannot be sorted or
    /// resized, and draws nothing for a row without details.
    /// </summary>
    /// <param name="title">The header text, empty by default.</param>
    /// <param name="id">Key for persisted column state.</param>
    /// <param name="width">The column's fixed width in pixels.</param>
    /// <param name="pin">Which edge the column is held at; a left pin keeps the expanders in view.</param>
    public static TableColumn<TRow, TRow> Expander(string title = "", string id = "expander", float width = 32f,
        TablePin pin = TablePin.None)
    {
        var column = Of(title, static (TRow row) => row, id, TableLength.Pixels(width), width, width, 0.5f, 0.5f, pin,
            renderer: new ExpanderRenderer<TRow>(), resizable: false);
        column.DisableSorting();
        return column;
    }
}

/// <summary>
/// A column that shows <typeparamref name="TValue"/> read from each row. The value is read for cells in view
/// only, compared without boxing, and its text is reused while the value stays equal.
/// </summary>
/// <typeparam name="TRow">The type of a row, as the source's indexer returns it.</typeparam>
/// <typeparam name="TValue">The type of the cell's value.</typeparam>
public sealed class TableColumn<TRow, TValue> : TableColumn<TRow>
{
    static readonly bool ComparableByDefault = IsComparable(typeof(TValue));

    readonly IComparer<TValue>? _order;

    /// <summary>Creates a column; see <see cref="TableColumn{TRow}.Of{TValue}"/> for the same with type inference.</summary>
    /// <param name="title">The header text.</param>
    /// <param name="value">Reads the cell's value from the row.</param>
    /// <param name="comparer">Orders the values; makes a column sortable whose type has no natural order.</param>
    /// <param name="renderer">Draws the cell; text of the value when null.</param>
    public TableColumn(string title, Func<TRow, TValue> value, IComparer<TValue>? comparer = null,
        ITableCellRenderer<TRow, TValue>? renderer = null)
        : base(title)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
        Comparer = comparer;
        Renderer = renderer;
        Sortable = comparer is not null || ComparableByDefault;
        _order = Sortable ? comparer ?? Comparer<TValue>.Default : null;
    }

    /// <summary>Reads the cell's value from the row.</summary>
    public Func<TRow, TValue> Value { get; }

    /// <summary>The comparer given when the column was built, or null to use the value type's own order.</summary>
    public IComparer<TValue>? Comparer { get; }

    /// <summary>Draws the cell, or null for the text of the value.</summary>
    public ITableCellRenderer<TRow, TValue>? Renderer { get; }

    internal override void RenderCell(Gui gui, TRow item, in TableCellFrame<TRow> frame)
    {
        var value = Value(item);
        if (Renderer is { } renderer)
        {
            renderer.Render(gui, new TableCellContext<TRow, TValue>(frame.Index, item, value, this, frame.FontSize,
                frame.Width, frame.Height, frame.Source, frame.State));
            return;
        }

        gui.DrawText(frame.Cache.TextCacheFor<TValue>(frame.Column).Text(frame.Cache, frame.Slot, value),
            frame.FontSize, centerInRect: false, clip: true);
    }

    /// <summary>Takes the column out of sorting even though its value type has an order.</summary>
    internal void DisableSorting()
    {
        Sortable = false;
    }

    internal override string Text(TRow item) => Value(item)?.ToString() ?? "";

    internal override int Compare(TRow left, TRow right) =>
        (_order ?? throw new InvalidOperationException($"Column '{Title}' cannot be sorted.")).Compare(
            Value(left), Value(right));

    /// <summary>Whether the type has a natural order: either comparison interface, or a nullable of such a type.</summary>
    static bool IsComparable(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return typeof(IComparable).IsAssignableFrom(type)
               || typeof(IComparable<>).MakeGenericType(type).IsAssignableFrom(type);
    }
}
