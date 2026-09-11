namespace Guinevere;

public partial class Gui
{
    private readonly Dictionary<Type, Dictionary<string, object>> _controlStates = new();

    /// <summary>
    /// Per-control state that survives between frames, owned by this <see cref="Gui"/> rather than by
    /// a static table. Two windows, or two tests, no longer share a control's open/closed or caret
    /// state just because the calls share a source line.
    /// </summary>
    /// <typeparam name="TState">The control's state type.</typeparam>
    /// <param name="id">The control's id, usually from <see cref="NodeId"/>.</param>
    /// <param name="create">Builds the state the first time this id is seen.</param>
    /// <returns>The state for this control.</returns>
    internal TState ControlState<TState>(string id, Func<TState> create) where TState : class
    {
        if (!_controlStates.TryGetValue(typeof(TState), out var store))
            _controlStates[typeof(TState)] = store = new Dictionary<string, object>();

        if (store.TryGetValue(id, out var existing)) return (TState)existing;

        var state = create();
        store[id] = state;
        return state;
    }

    /// <summary>Forgets every control's remembered state.</summary>
    public void ClearControlStates() => _controlStates.Clear();

    /// <summary>Forgets the remembered state of one kind of control.</summary>
    /// <typeparam name="TState">The control's state type.</typeparam>
    internal void ClearControlStates<TState>() where TState : class =>
        _controlStates.Remove(typeof(TState));
}
