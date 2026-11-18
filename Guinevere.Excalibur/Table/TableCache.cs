using System.Globalization;

namespace Guinevere;

/// <summary>The strings a table keeps for one built row, so a row that stays in view costs nothing to rebuild.</summary>
sealed class TableRowSlot
{
    public int Row = -1;
    public int Generation = -1;
    public string? Id;
    public string? PadLeftId;
    public string? PadRightId;
    public string?[] CellIds = [];
    public object?[] Values = [];
    public string?[] Texts = [];
}

/// <summary>
/// What a table derives from its inputs and reuses until they change: the visible columns and their
/// layout, the node ids of built rows and cells, and the text of cell values. Nothing here is read from the
/// data source except through <see cref="Text"/>, which only reuses a string while the value is equal.
/// </summary>
sealed class TableCache
{
    readonly Dictionary<(TableColumnId Id, float FontSize, float Padding), float> _autoWidths = [];

    TableColumn[] _snapshot = [];
    TableRowSlot?[] _slots = [];
    TableLayout? _layout;
    float _layoutWidth;
    float _layoutFontSize;
    float _layoutPadding;
    int _generation;
    CultureInfo? _culture;

    /// <summary>The columns laid out: those visible by default, in the order given.</summary>
    public TableColumn[] Shown { get; private set; } = [];

    /// <summary>Node id of each shown column's header cell.</summary>
    public string[] HeaderIds { get; private set; } = [];

    string[] CellSuffixes { get; set; } = [];

    /// <summary>
    /// Starts a frame: rebuilds what depends on the column list when it differs from the last frame's, and
    /// drops cached text when the culture changed. The list is compared by content, so a caller may share
    /// one list or edit it in place.
    /// </summary>
    /// <param name="columns">The caller's column definitions.</param>
    public void Begin(IReadOnlyList<TableColumn> columns)
    {
        if (!ReferenceEquals(_culture, CultureInfo.CurrentCulture))
        {
            _culture = CultureInfo.CurrentCulture;
            _generation++;
        }

        if (SameColumns(columns))
        {
            return;
        }

        var ids = new HashSet<TableColumnId>();
        var shown = new List<TableColumn>(columns.Count);
        foreach (var column in columns)
        {
            if (!ids.Add(column.Id))
            {
                throw new ArgumentException($"Table column id '{column.Id}' is used twice.", nameof(columns));
            }

            if (column.InitiallyVisible)
            {
                shown.Add(column);
            }
        }

        _snapshot = [.. columns];
        Shown = [.. shown];
        HeaderIds = [.. Shown.Select(static column => "table/header/" + column.Id.Value)];
        CellSuffixes = [.. Shown.Select(static column => "/" + column.Id.Value)];
        _layout = null;
        _generation++;
    }

    bool SameColumns(IReadOnlyList<TableColumn> columns)
    {
        if (columns.Count != _snapshot.Length)
        {
            return false;
        }

        for (var i = 0; i < _snapshot.Length; i++)
        {
            if (!ReferenceEquals(columns[i], _snapshot[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The layout resolved for these inputs, when nothing it depends on has changed.</summary>
    public bool TryLayout(float width, float fontSize, float padding, out TableLayout layout)
    {
        layout = _layout!;
        return _layout is not null && _layoutWidth == width && _layoutFontSize == fontSize
               && _layoutPadding == padding;
    }

    /// <summary>Keeps a resolved layout for the inputs it was resolved from.</summary>
    public void StoreLayout(TableLayout layout, float width, float fontSize, float padding)
    {
        _layout = layout;
        _layoutWidth = width;
        _layoutFontSize = fontSize;
        _layoutPadding = padding;
    }

    /// <summary>A column's header-sized width, when it was measured for this text size and padding.</summary>
    public bool TryAutoWidth(TableColumnId id, float fontSize, float padding, out float width) =>
        _autoWidths.TryGetValue((id, fontSize, padding), out width);

    /// <summary>Remembers a column's header-sized width; a definition never changes, so it stays valid.</summary>
    public void StoreAutoWidth(TableColumnId id, float fontSize, float padding, float width) =>
        _autoWidths[(id, fontSize, padding)] = width;

    /// <summary>Makes room to remember every row built in a frame, with as many again for rows that just left.</summary>
    /// <param name="rowsBuilt">Rows built this frame.</param>
    public void Reserve(int rowsBuilt)
    {
        var capacity = Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, rowsBuilt * 2)));
        if (_slots.Length < capacity)
        {
            _slots = new TableRowSlot?[capacity];
        }
    }

    /// <summary>The slot of a built row, emptied when it last held another row or other columns.</summary>
    public TableRowSlot Slot(int row)
    {
        var slot = _slots[row & (_slots.Length - 1)] ??= new TableRowSlot();
        if (slot.Row == row && slot.Generation == _generation)
        {
            return slot;
        }

        slot.Row = row;
        slot.Generation = _generation;
        slot.Id = slot.PadLeftId = slot.PadRightId = null;
        if (slot.CellIds.Length == Shown.Length)
        {
            Array.Clear(slot.CellIds);
            Array.Clear(slot.Values);
            Array.Clear(slot.Texts);
        }
        else
        {
            slot.CellIds = new string?[Shown.Length];
            slot.Values = new object?[Shown.Length];
            slot.Texts = new string?[Shown.Length];
        }

        return slot;
    }

    public static string RowId(TableRowSlot slot) =>
        slot.Id ??= "table/row" + slot.Row.ToString(CultureInfo.InvariantCulture);

    public static string PadLeftId(TableRowSlot slot) => slot.PadLeftId ??= RowId(slot) + "/padLeft";

    public static string PadRightId(TableRowSlot slot) => slot.PadRightId ??= RowId(slot) + "/padRight";

    /// <summary>Node id of one cell: its row's id and its column's key.</summary>
    public string CellId(TableRowSlot slot, int column) =>
        slot.CellIds[column] ??= RowId(slot) + CellSuffixes[column];

    /// <summary>
    /// The text of a cell value. Strings are their own text. Value types are formatted once and reused while
    /// the value stays equal, which is safe because a boxed value cannot change in place. Anything else may
    /// change behind its reference, so it is formatted every time.
    /// </summary>
    public string Text(TableRowSlot slot, int column, object? value)
    {
        switch (value)
        {
            case null:
                return "";
            case string text:
                return text;
            case ValueType:
                if (slot.Texts[column] is { } cached && value.Equals(slot.Values[column]))
                {
                    return cached;
                }

                var formatted = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
                slot.Values[column] = value;
                slot.Texts[column] = formatted;
                return formatted;
            default:
                return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
        }
    }
}
