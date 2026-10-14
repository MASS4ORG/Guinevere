namespace Guinevere;

/// <summary>The lifecycle stage of a touch or pen contact.</summary>
public enum TouchPhase
{
    /// <summary>A contact began.</summary>
    Began,
    /// <summary>A contact moved.</summary>
    Moved,
    /// <summary>A contact remains held.</summary>
    Stationary,
    /// <summary>A contact ended normally.</summary>
    Ended,
    /// <summary>The host canceled a contact.</summary>
    Canceled
}

/// <summary>A contact in window coordinates; pressure is null when the host cannot supply it.</summary>
public readonly record struct TouchContact(long Id, Vector2 Position, float? Pressure, TouchPhase Phase);

/// <summary>A recognized touch gesture.</summary>
public enum TouchGestureKind
{
    /// <summary>A short stationary contact.</summary>
    Tap,
    /// <summary>A held stationary contact.</summary>
    LongPress,
    /// <summary>A contact released after directional motion.</summary>
    Swipe,
    /// <summary>A change in distance between two contacts.</summary>
    Pinch,
    /// <summary>A change in angle between two contacts.</summary>
    Rotate
}

/// <summary>A gesture; scale is a ratio and rotation is a delta in radians.</summary>
public readonly record struct TouchGesture(TouchGestureKind Kind, long ContactId, Vector2 Position,
    Vector2 Delta = default, float Scale = 1, float Rotation = 0);

/// <summary>Optional multi-contact input and host-provided gestures for the current frame.</summary>
public interface ITouchCapability : IPlatformCapability
{
    /// <summary>Contact updates in chronological order, retaining end and cancellation edges.</summary>
    IReadOnlyList<TouchContact> Contacts { get; }
    /// <summary>High-level gestures supplied by the host.</summary>
    IReadOnlyList<TouchGesture> Gestures { get; }
}

/// <summary>Accumulates contact and gesture events until a rendered frame consumes them.</summary>
public sealed class TouchInputState : ITouchCapability
{
    readonly List<TouchContact> _contacts = [];
    readonly List<TouchGesture> _gestures = [];
    readonly Dictionary<long, TouchContact> _active = new();
    /// <summary>Whether contact or gesture updates are waiting for a frame.</summary>
    public bool Changed { get; private set; }
    /// <summary>Whether contacts remain held.</summary>
    public bool HasActiveContacts => _active.Count > 0;
    /// <inheritdoc />
    public IReadOnlyList<TouchContact> Contacts => _contacts.AsReadOnly();
    /// <inheritdoc />
    public IReadOnlyList<TouchGesture> Gestures => _gestures.AsReadOnly();
    /// <summary>Queues one contact update.</summary>
    public void AddContact(TouchContact contact)
    {
        _contacts.Add(contact);
        if (contact.Phase is TouchPhase.Ended or TouchPhase.Canceled)
        {
            _active.Remove(contact.Id);
        }
        else
        {
            _active[contact.Id] = contact;
        }

        Changed = true;
    }
    /// <summary>Queues one high-level host gesture.</summary>
    public void AddGesture(TouchGesture gesture)
    {
        _gestures.Add(gesture);
        Changed = true;
    }

    /// <summary>Turns a backend's live-contact snapshot into begin, move and end edges.</summary>
    public void SynchronizeContacts(IReadOnlyList<TouchContact> contacts)
    {
        var seen = new HashSet<long>();
        foreach (var contact in contacts)
        {
            seen.Add(contact.Id);
            var exists = _active.TryGetValue(contact.Id, out var previous);
            if (exists && previous.Position == contact.Position && previous.Pressure == contact.Pressure)
            {
                continue;
            }

            AddContact(contact with { Phase = exists ? TouchPhase.Moved : TouchPhase.Began });
        }
        foreach (var contact in _active.Values.ToArray())
        {
            if (!seen.Contains(contact.Id))
            {
                AddContact(contact with { Phase = TouchPhase.Ended });
            }
        }
    }
    /// <summary>Clears updates after both frame passes.</summary>
    public void NewFrame()
    {
        _contacts.Clear();
        _gestures.Clear();
        Changed = false;
    }
}
