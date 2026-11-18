using System.Runtime.CompilerServices;

namespace Guinevere;

public partial class Gui
{
    readonly Dictionary<Type, object> _dataStores = new();
    readonly Stack<int> _openDataScopes = new();
    int _dataScopeToken;
    ulong _dataFrame;
    readonly record struct AutomaticDataScopeKey(int Identity);

    /// <summary>
    /// The number of the frame being built. It goes up by one for every frame that starts, and is the same in both
    /// passes of that frame, so state that is published once per frame can tell whether it already was.
    /// </summary>
    public long FrameNumber => (long)_dataFrame;

    internal int CurrentDataScope => LayoutNodeScopeStack.TryPeek(out var scope) ? scope.DataScope : 0;

    /// <summary>Selects a keyed data context inherited by this node's descendants and restored on node exit.</summary>
    public void SetDataScope<T>(T key) where T : notnull =>
        CurrentNodeScope.DataScope = _nodeIdentities.DataScope(CurrentNodeScope.InheritedDataScope, _itemKey, key);

    /// <summary>Selects a named or automatic data context relative to the context this node inherited.</summary>
    public void SetDataScope(string? key = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        if (key is not null)
        {
            SetDataScope(key);
        }
        else
        {
            var identity = _nodeIdentities.Automatic(CurrentNode.Identity, _itemKey, filePath, lineNumber,
                control: true, data: CurrentNodeScope.InheritedDataScope);
            _nodeIdentities.Pin(identity);
            SetDataScope(new AutomaticDataScopeKey(identity));
        }
    }

    /// <summary>Enters a nested keyed data context; equal typed keys intentionally share data in the same context.</summary>
    public DataScope EnterDataScope<T>(T key) where T : notnull
    {
        var node = CurrentNodeScope;
        var previous = node.DataScope;
        node.DataScope = _nodeIdentities.DataScope(previous, _itemKey, key);
        var token = checked(++_dataScopeToken);
        _openDataScopes.Push(token);
        return new DataScope(this, node, previous, token);
    }

    /// <summary>Enters an automatically identified data context using the parent, item key and call occurrence.</summary>
    public DataScope EnterDataScope(string? key = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        return key is null
            ? EnterDataScope(new AutomaticDataScopeKey(DataIdentity(null, filePath, lineNumber)))
            : EnterDataScope(key);
    }

    internal void ExitDataScope(LayoutNodeScope node, int previous, int token)
    {
        if (!_openDataScopes.TryPeek(out var active) || active != token || CurrentNodeScope != node)
        {
            throw new InvalidOperationException("Data scopes must be disposed in reverse order within their node.");
        }

        _openDataScopes.Pop();
        node.DataScope = previous;
    }

    /// <summary>Gets a stable reference to persistent scoped data, automatically keyed when no ID is supplied.</summary>
    /// <remarks>Named IDs share within the data and item context; references survive store growth until ClearData.</remarks>
    public ref T GetData<T>(T defaultValue, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        ref GetDataCell(defaultValue, DataIdentity(id, filePath, lineNumber)).Value;

    /// <summary>Sets persistent scoped data; supply the same named ID to share it with another call site.</summary>
    public void SetData<T>(T value, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) =>
        GetDataCell(value, DataIdentity(id, filePath, lineNumber)).Value = value;

    /// <summary>Updates persistent data once per frame and returns the first update's value in both passes.</summary>
    public T UpdateData<T>(T defaultValue, Func<T, T> update, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(update);
        var cell = GetDataCell(defaultValue, DataIdentity(id, filePath, lineNumber));
        if (cell.Frame != _dataFrame)
        {
            cell.Snapshot = cell.Value = update(cell.Value);
            cell.Frame = _dataFrame;
        }
        return cell.Snapshot;
    }

    /// <summary>Forgets all scoped data without disposing caller-owned values; existing references become detached.</summary>
    public void ClearData() => _dataStores.Clear();

    /// <summary>
    /// The identity persistent data (data cells, springs, tweens, automatic ids) is kept under; it is pinned, so it and
    /// the identities it depends on keep their keys however long the call site goes unused.
    /// </summary>
    internal int DataIdentity(string? id, string filePath, int lineNumber)
    {
        var identity = id is null
            ? _nodeIdentities.Automatic(CurrentNode.Identity, _itemKey, filePath, lineNumber,
                control: true, data: CurrentDataScope)
            : _nodeIdentities.Explicit(0, _itemKey, id, CurrentDataScope);
        _nodeIdentities.Pin(identity);
        return identity;
    }

    DataCell<T> GetDataCell<T>(T defaultValue, int identity)
    {
        if (!_dataStores.TryGetValue(typeof(T), out var table))
        {
            _dataStores.Add(typeof(T), table = new Dictionary<int, DataCell<T>>());
        }

        var store = (Dictionary<int, DataCell<T>>)table;
        if (!store.TryGetValue(identity, out var cell))
        {
            store.Add(identity, cell = new DataCell<T>(defaultValue));
        }

        return cell;
    }

    void BeginDataPass()
    {
        if (_openDataScopes.Count != 0)
        {
            throw new InvalidOperationException("Data scopes must end before changing passes.");
        }

        RootNode?.Scope.ResetDataScope(0);
    }

    sealed class DataCell<T>(T value) : IDataCell
    {
        public object? InspectValue => Value;
        internal T Value = value;
        internal T Snapshot = value;
        internal ulong Frame = ulong.MaxValue;
    }
}
