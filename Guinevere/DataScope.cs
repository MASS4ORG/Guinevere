namespace Guinevere;

/// <summary>Restores the enclosing GUI data scope when disposed in reverse order.</summary>
public readonly struct DataScope : IDisposable
{
    readonly Gui? _gui;
    readonly LayoutNodeScope? _nodeScope;
    readonly int _previous;
    readonly int _token;

    internal DataScope(Gui gui, LayoutNodeScope nodeScope, int previous, int token)
    {
        _gui = gui;
        _nodeScope = nodeScope;
        _previous = previous;
        _token = token;
    }

    /// <summary>Restores the data scope active before entry; stored values remain alive.</summary>
    public void Dispose() => _gui?.ExitDataScope(_nodeScope!, _previous, _token);
}
