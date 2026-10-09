namespace Guinevere;

/// <summary>Restores the enclosing item identity when a keyed collection item leaves scope.</summary>
public readonly struct ItemKeyScope : IDisposable
{
    readonly Gui? _gui;
    readonly int _previous;
    readonly int _current;

    internal ItemKeyScope(Gui gui, int previous, int current)
    {
        _gui = gui;
        _previous = previous;
        _current = current;
    }

    /// <summary>Ends this item scope; nested scopes must be disposed in reverse order.</summary>
    public void Dispose() => _gui?.ExitItemKey(_current, _previous);
}
