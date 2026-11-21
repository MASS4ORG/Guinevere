using System.Globalization;

namespace Guinevere;

/// <summary>
/// What a <c>ButtonGroup</c> remembers between frames: the item that is the group's single Tab stop, whether an arrow
/// key just moved it, and the item flags its key handler needs. The caller keeps the selection itself.
/// </summary>
sealed class ButtonGroupState
{
    Action<KeyDownEvent>? _keyHandler;
    string? _idGroup;
    string?[] _idSources = [];
    string?[] _ids = [];

    /// <summary>The item holding the roving tab stop, or -1 when no item is enabled.</summary>
    internal int Active { get; set; } = -1;

    /// <summary>An arrow key moved <see cref="Active"/>; the item takes keyboard focus when it next renders.</summary>
    internal bool FocusPending { get; set; }

    /// <summary>Whether the items stack vertically, which decides the arrow keys the group answers to.</summary>
    internal bool Vertical { get; private set; }

    /// <summary>Whether each item can be reached, folding the group's own enabled flag into the item's.</summary>
    internal bool[] Enabled { get; private set; } = [];

    /// <summary>The listener for keys bubbling up from the focused item; created once and reused every frame.</summary>
    internal Action<KeyDownEvent> KeyHandler => _keyHandler ??= OnKey;

    /// <summary>
    /// Refreshes the per-frame inputs and settles the tab stop: while focus is outside the group it rests on the
    /// selected item (single-select), else it stays where it was, always landing on an enabled item.
    /// </summary>
    /// <param name="items">The group's items.</param>
    /// <param name="groupEnabled">Whether the whole group is enabled.</param>
    /// <param name="vertical">Whether the items stack vertically.</param>
    /// <param name="selected">The selected item of a single-select group, otherwise -1.</param>
    /// <param name="focusWithin">Whether focus is on one of the items.</param>
    internal void Begin(IReadOnlyList<ButtonGroupItem> items, bool groupEnabled, bool vertical, int selected,
        bool focusWithin)
    {
        Vertical = vertical;
        RefreshEnabled(items, groupEnabled);
        var preferred = !focusWithin && IsReachable(selected) ? selected : Active;
        Active = IsReachable(preferred) ? preferred : EdgeEnabled(Enabled, last: false);
    }

    /// <summary>Sizes the per-item arrays to the item count and folds the group's enabled flag into each item's.</summary>
    void RefreshEnabled(IReadOnlyList<ButtonGroupItem> items, bool groupEnabled)
    {
        if (Enabled.Length != items.Count)
        {
            Enabled = new bool[items.Count];
            _ids = new string?[items.Count];
            _idSources = new string?[items.Count];
        }

        for (var i = 0; i < Enabled.Length; i++)
        {
            Enabled[i] = groupEnabled && items[i].Enabled;
        }
    }

    /// <summary>Whether <paramref name="index"/> names an enabled item.</summary>
    bool IsReachable(int index) => index >= 0 && index < Enabled.Length && Enabled[index];

    /// <summary>
    /// The node id of an item, built once and reused while the group id and the item's own id stay the same, so a
    /// steady group allocates no id strings per frame.
    /// </summary>
    /// <param name="group">The group's node id.</param>
    /// <param name="itemId">The item's own id, or null to use its index.</param>
    /// <param name="index">The item's index; <see cref="Begin"/> has sized the cache for it.</param>
    internal string ItemId(string group, string? itemId, int index)
    {
        if (!string.Equals(_idGroup, group, StringComparison.Ordinal))
        {
            _idGroup = group;
            Array.Clear(_ids);
        }

        if (_ids[index] is { } cached && string.Equals(_idSources[index], itemId, StringComparison.Ordinal))
        {
            return cached;
        }

        _idSources[index] = itemId;
        return _ids[index] = $"{group}/{itemId ?? index.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// The enabled item reached from <paramref name="from"/> by <paramref name="step"/> (+1 or -1) while wrapping at
    /// the ends, or -1 when no item is enabled.
    /// </summary>
    internal static int NextEnabled(ReadOnlySpan<bool> enabled, int from, int step)
    {
        for (var offset = 1; offset <= enabled.Length; offset++)
        {
            var index = ((from + (offset * step)) % enabled.Length + enabled.Length) % enabled.Length;
            if (enabled[index])
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The first or last enabled item, or -1 when none is enabled.</summary>
    internal static int EdgeEnabled(ReadOnlySpan<bool> enabled, bool last)
    {
        for (var i = 0; i < enabled.Length; i++)
        {
            var index = last ? enabled.Length - 1 - i : i;
            if (enabled[index])
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Moves the tab stop with the arrows along the group's axis, Home and End; the cross-axis arrows are left to
    /// ordinary focus navigation so they leave the group.
    /// </summary>
    void OnKey(KeyDownEvent e)
    {
        var next = TargetFor(e.Key);
        if (next < 0)
        {
            return;
        }

        Active = next;
        FocusPending = true;
        e.PreventDefault();
        e.StopPropagation();
    }

    /// <summary>The item <paramref name="key"/> moves the tab stop to, or -1 when the group leaves the key alone.</summary>
    int TargetFor(KeyboardKey key)
    {
        var step = AxisStep(key);
        if (step != 0)
        {
            return NextEnabled(Enabled, Active, step);
        }

        return key switch
        {
            KeyboardKey.Home => EdgeEnabled(Enabled, last: false),
            KeyboardKey.End => EdgeEnabled(Enabled, last: true),
            _ => -1
        };
    }

    /// <summary>-1 or +1 for the previous or next arrow along the group's axis, otherwise 0.</summary>
    int AxisStep(KeyboardKey key) => (key, Vertical) switch
    {
        (KeyboardKey.Left, false) or (KeyboardKey.Up, true) => -1,
        (KeyboardKey.Right, false) or (KeyboardKey.Down, true) => 1,
        _ => 0
    };
}
