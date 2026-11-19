namespace Guinevere;

/// <summary>Something that holds values under keys of a <see cref="NodeIdentityKeySpace"/> and forgets them on eviction.</summary>
interface INodeIdentityKeyOwner
{
    /// <summary>Forgets the value stored under <paramref name="key"/>.</summary>
    void Release(int key);
}

/// <summary>
/// Hands out small integer keys and evicts the ones not used for a while. Key 0 is never handed out. A freed key is
/// reused only after a quarantine, so an integer still held for a frame or two never names a different value.
/// </summary>
sealed class NodeIdentityKeySpace
{
    const int QuarantinePasses = 16;

    readonly Queue<(int Key, long Freed)> _free = new();
    int[] _lastUse = new int[64];
    bool[] _pinned = new bool[64];
    INodeIdentityKeyOwner?[] _owners = new INodeIdentityKeyOwner?[64];
    int _next = 1;

    /// <summary>Number of keys in use.</summary>
    internal int Count { get; private set; }

    /// <summary>One past the highest key ever handed out; arrays indexed by key need at least this length.</summary>
    internal int Capacity => _next;

    /// <summary>A key for a new value of <paramref name="owner"/>, used now.</summary>
    internal int Allocate(INodeIdentityKeyOwner owner, int pass)
    {
        int key;
        if (_free.TryPeek(out var head) && head.Freed + QuarantinePasses <= pass)
        {
            key = _free.Dequeue().Key;
        }
        else
        {
            key = _next++;
            if (key >= _lastUse.Length)
            {
                var size = _lastUse.Length * 2;
                Array.Resize(ref _lastUse, size);
                Array.Resize(ref _pinned, size);
                Array.Resize(ref _owners, size);
            }
        }

        _owners[key] = owner;
        _pinned[key] = false;
        _lastUse[key] = pass;
        Count++;
        return key;
    }

    /// <summary>Marks <paramref name="key"/> used in <paramref name="pass"/>.</summary>
    internal void Touch(int key, int pass) => _lastUse[key] = pass;

    /// <summary>Keeps <paramref name="key"/> forever; false when it was kept already.</summary>
    internal bool Pin(int key)
    {
        if (key == 0 || _pinned[key])
        {
            return false;
        }

        _pinned[key] = true;
        return true;
    }

    /// <summary>Whether <paramref name="key"/> is kept forever.</summary>
    internal bool IsPinned(int key) => _pinned[key];

    /// <summary>Evicts every unpinned key last used before <paramref name="horizon"/>.</summary>
    internal void Sweep(int pass, int horizon)
    {
        for (var key = 1; key < _next; key++)
        {
            if (_owners[key] is { } owner && !_pinned[key] && _lastUse[key] < horizon)
            {
                owner.Release(key);
                _owners[key] = null;
                _free.Enqueue((key, pass));
                Count--;
            }
        }
    }
}

/// <summary>
/// Maps values to keys of a shared <see cref="NodeIdentityKeySpace"/> and back; <paramref name="released"/> hears of
/// every eviction so parallel per-key data can be cleared.
/// </summary>
sealed class NodeIdentityInterner<T>(NodeIdentityKeySpace keys, Action<int>? released = null)
    : INodeIdentityKeyOwner where T : notnull
{
    readonly Dictionary<T, int> _keys = [];
    T[] _values = new T[64];

    /// <summary>The key of <paramref name="value"/>, interning it on first sight, and marks it used.</summary>
    internal int Intern(T value, int pass)
    {
        if (_keys.TryGetValue(value, out var key))
        {
            keys.Touch(key, pass);
            return key;
        }

        key = keys.Allocate(this, pass);
        if (key >= _values.Length)
        {
            Array.Resize(ref _values, Math.Max(_values.Length * 2, keys.Capacity));
        }

        _values[key] = value;
        _keys.Add(value, key);
        return key;
    }

    /// <summary>The value stored under <paramref name="key"/>.</summary>
    internal T this[int key] => _values[key];

    /// <inheritdoc />
    public void Release(int key)
    {
        _keys.Remove(_values[key]);
        _values[key] = default!;
        released?.Invoke(key);
    }
}
