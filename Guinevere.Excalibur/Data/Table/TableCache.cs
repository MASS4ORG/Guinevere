using System.Globalization;

namespace Guinevere;

/// <summary>The strings a table keeps for one built row, so a row that stays in view costs nothing to rebuild.</summary>
sealed class TableRowSlot(int index)
{
    /// <summary>The slot's place in the ring, which typed caches index their arrays by.</summary>
    public readonly int Index = index;

    public int Row = -1;
    public int Generation = -1;
    public int SourceVersion = -1;

    /// <summary>The row's identity key, read from the source once while the slot holds this row.</summary>
    public string? Key;

    public string? Id;
    public string? DetailsId;
    public string? PadLeftId;
    public string? PadRightId;
    public string? PinnedLeftId;
    public string? PinnedRightId;
    public string?[] CellIds = [];
}

/// <summary>
/// What a table derives from its inputs and reuses until they change: the column geometry, the node ids of built
/// rows and cells, and the text of cell values. Reusing text never reads the source; it only compares the value
/// the column just read with the one the text was made from.
/// </summary>
sealed class TableCache
{
    readonly Dictionary<(TableColumn Column, float FontSize, float Padding), float> _autoWidths = [];

    TableColumnSet? _columns;
    TableColumnView? _view;
    int _stateVersion = -1;
    TableRowSlot?[] _slots = [];
    object?[] _textCaches = [];
    TableLayout? _layout;
    float _layoutWidth;
    float _layoutFontSize;
    float _layoutPadding;
    CultureInfo? _culture;

    /// <summary>The columns of the table that last drew, or null before the first frame.</summary>
    public TableColumnSet? Columns => _columns;

    /// <summary>The arrangement of the columns this frame lays out, or null before the first frame.</summary>
    public TableColumnView? View => _view;

    /// <summary>Incremented when cached text or ids stop being valid: new columns, a new arrangement or a new culture.</summary>
    public int Generation { get; private set; }

    /// <summary>How many rows the cache can remember at once.</summary>
    public int Capacity => _slots.Length;

    /// <summary>
    /// The version of the source's data this frame is built from. A slot remembers the row's identity and the
    /// ids made from it, so it is emptied when the version moves: after a sort or a change the same index
    /// holds another row.
    /// </summary>
    public int SourceVersion { get; set; }

    /// <summary>
    /// Starts a frame: drops cached text when the culture changed, and everything that depends on the columns
    /// when they are not the columns of the last frame or the user's choices about them changed. Columns are
    /// frozen, so one reference comparison and one version comparison decide.
    /// </summary>
    /// <param name="columns">The table's columns.</param>
    /// <param name="choices">What the user chose about the columns.</param>
    /// <returns>Whether the columns, not only their arrangement, changed since the last frame.</returns>
    public bool Begin(TableColumnSet columns, TableColumnState choices)
    {
        if (!ReferenceEquals(_culture, CultureInfo.CurrentCulture))
        {
            _culture = CultureInfo.CurrentCulture;
            Generation++;
        }

        var replaced = !ReferenceEquals(_columns, columns);
        if (!replaced && _stateVersion == choices.Version)
        {
            return false;
        }

        if (replaced)
        {
            _columns = columns;
            _autoWidths.Clear();
        }

        choices.Bind(columns);
        _stateVersion = choices.Version;
        _view = columns.Arrange(choices);
        _textCaches = new object?[_view.Shown.Length];
        _layout = null;
        Generation++;
        return replaced;
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
    public bool TryAutoWidth(TableColumn column, float fontSize, float padding, out float width) =>
        _autoWidths.TryGetValue((column, fontSize, padding), out width);

    /// <summary>Remembers a column's header-sized width; a definition never changes, so it stays valid.</summary>
    public void StoreAutoWidth(TableColumn column, float fontSize, float padding, float width) =>
        _autoWidths[(column, fontSize, padding)] = width;

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
        var index = row & (_slots.Length - 1);
        var slot = _slots[index] ??= new TableRowSlot(index);
        if (slot.Row == row && slot.Generation == Generation && slot.SourceVersion == SourceVersion)
        {
            return slot;
        }

        slot.Row = row;
        slot.Generation = Generation;
        slot.SourceVersion = SourceVersion;
        slot.Key = null;
        slot.Id = slot.DetailsId = slot.PadLeftId = slot.PadRightId = slot.PinnedLeftId = slot.PinnedRightId = null;
        var shown = _view!.Shown.Length;
        if (slot.CellIds.Length == shown)
        {
            Array.Clear(slot.CellIds);
        }
        else
        {
            slot.CellIds = new string?[shown];
        }

        return slot;
    }

    /// <summary>
    /// The identity key of the slot's row, asked of the source the first time and kept until the slot holds
    /// another row or the source's data changes, so a row that stays in view costs the source nothing.
    /// </summary>
    /// <typeparam name="TRow">The source's row type.</typeparam>
    public static string Key<TRow>(TableRowSlot slot, ITableViewSource<TRow> source) =>
        slot.Key ??= source.GetRowIdentity(slot.Row).Key;

    /// <summary>
    /// Node id of a row: its identity key, so focus, hover and anything a cell keeps follow the row through a
    /// sort or an insert. A key's slash and percent are escaped, so no key can read as a child id.
    /// </summary>
    public static string RowId(TableRowSlot slot) =>
        slot.Id ??= "table/row" + (slot.Key is { } key ? Escape(key) : slot.Row.ToString(CultureInfo.InvariantCulture));

    static string Escape(string key) => key.AsSpan().IndexOfAny('/', '%') < 0
        ? key
        : key.Replace("%", "%25", StringComparison.Ordinal).Replace("/", "%2F", StringComparison.Ordinal);

    /// <summary>The node id of the details below an expanded row.</summary>
    public static string DetailsId(TableRowSlot slot) => slot.DetailsId ??= RowId(slot) + "/details";

    public static string PadLeftId(TableRowSlot slot) => slot.PadLeftId ??= RowId(slot) + "/padLeft";

    public static string PadRightId(TableRowSlot slot) => slot.PadRightId ??= RowId(slot) + "/padRight";

    /// <summary>The node id of a row's strip of pinned cells on one side.</summary>
    public static string PinnedId(TableRowSlot slot, TablePin pin) => pin == TablePin.Left
        ? slot.PinnedLeftId ??= RowId(slot) + "/pinned-left"
        : slot.PinnedRightId ??= RowId(slot) + "/pinned-right";

    /// <summary>
    /// Node id of one cell: its row's id and its column's key. A pinned cell lives in its row's strip on its
    /// side, so its id carries that strip's name.
    /// </summary>
    public string CellId(TableRowSlot slot, int column)
    {
        var pin = _view!.Shown[column].Pin;
        var container = pin == TablePin.None ? RowId(slot) : PinnedId(slot, pin);
        return slot.CellIds[column] ??= container + _view.CellSuffixes[column];
    }

    /// <summary>The text cache of one shown column, made the first time the column draws.</summary>
    /// <typeparam name="TValue">The column's value type.</typeparam>
    /// <param name="column">The column's position among the shown columns.</param>
    public TableTextCache<TValue> TextCacheFor<TValue>(int column)
    {
        if (_textCaches[column] is TableTextCache<TValue> cache)
        {
            return cache;
        }

        var created = new TableTextCache<TValue>();
        _textCaches[column] = created;
        return created;
    }
}

/// <summary>
/// The text of one column's cell values. Strings are their own text. A value type is formatted once and the
/// text reused while the value stays equal, compared without boxing, which is safe because a value cannot
/// change behind a copy. Any other type may change behind its reference, so it is formatted every time.
/// </summary>
/// <typeparam name="TValue">The column's value type.</typeparam>
sealed class TableTextCache<TValue>
{
    static readonly bool Reusable = typeof(TValue).IsValueType;
    static readonly bool IsNullable = Nullable.GetUnderlyingType(typeof(TValue)) is not null;

    TValue[] _values = [];
    string?[] _texts = [];
    int[] _rows = [];
    int _generation = -1;

    /// <summary>The text of a value in the cell of a built row.</summary>
    /// <param name="cache">The table's cache, which says when stored text is stale.</param>
    /// <param name="slot">The slot of the cell's row.</param>
    /// <param name="value">The value the column just read.</param>
    public string Text(TableCache cache, TableRowSlot slot, TValue value)
    {
        if (!Reusable)
        {
            // A reference type: a null check and a cast box nothing.
            if (value is null)
            {
                return "";
            }

            return typeof(TValue) == typeof(string) ? (string)(object)value : value.ToString() ?? "";
        }

        // A value type, where a null test would box it when the JIT has not optimized the method yet: only a
        // Nullable<T> can be missing, and it equals its default exactly then.
        if (IsNullable && EqualityComparer<TValue>.Default.Equals(value, default))
        {
            return "";
        }

        if (_values.Length != cache.Capacity || _generation != cache.Generation)
        {
            _values = new TValue[cache.Capacity];
            _texts = new string?[cache.Capacity];
            _rows = new int[cache.Capacity];
            Array.Fill(_rows, -1);
            _generation = cache.Generation;
        }

        var index = slot.Index;
        if (_rows[index] == slot.Row && _texts[index] is { } text
                                     && EqualityComparer<TValue>.Default.Equals(_values[index], value))
        {
            return text;
        }

        // A constrained call: no boxing for a struct that overrides ToString, and the current culture
        // formats it, as the text always has been.
        text = value!.ToString() ?? "";
        _values[index] = value;
        _texts[index] = text;
        _rows[index] = slot.Row;
        return text;
    }
}
