namespace Guinevere;

public partial class Gui
{
    readonly NodeIdentityRegistry _nodeIdentities = new();
    readonly HashSet<int> _submittedNodes = [];
    int _itemKey;

    /// <summary>Scopes automatic node and control identity to a stable typed collection key in both passes.</summary>
    /// <remarks>Keys use value equality and must be unique within the parent and enclosing item scope per pass.</remarks>
    public ItemKeyScope ItemKey<T>(T key) where T : notnull
    {
        var previous = _itemKey;
        _itemKey = _nodeIdentities.EnterItem(CurrentNode.Identity, previous, key, CurrentDataScope);
        return new ItemKeyScope(this, previous, _itemKey);
    }

    internal void ExitItemKey(int current, int previous)
    {
        if (_itemKey != current) throw new InvalidOperationException("Item scopes must be disposed in reverse order.");
        _itemKey = previous;
    }

    internal int NodeIdentity(string? id, string filePath, int lineNumber, LayoutNode? parent) =>
        id is null
            ? _nodeIdentities.Automatic(parent?.Identity ?? 0, _itemKey, filePath, lineNumber, data: CurrentDataScope)
            : _nodeIdentities.Explicit(parent?.Identity ?? 0, _itemKey, id, CurrentDataScope);

    internal string IdentityName(int identity) => _nodeIdentities.Name(identity);

    /// <summary>Returns a cached automatic ID for APIs that require strings, using call site and occurrence.</summary>
    public string AutomaticId(
        [System.Runtime.CompilerServices.CallerFilePath] string filePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = 0) =>
        IdentityName(DataIdentity(null, filePath, lineNumber));

    void SubmitIdentity(int identity)
    {
        if (!_submittedNodes.Add(identity))
            throw new InvalidOperationException(
                "An explicit node ID must be unique within its parent, item and data scope.");
    }

    void BeginIdentityPass()
    {
        if (_itemKey != 0) throw new InvalidOperationException("Item scopes must end before changing passes.");
        BeginDataPass();
        _nodeIdentities.BeginPass();
        _submittedNodes.Clear();
    }
}
