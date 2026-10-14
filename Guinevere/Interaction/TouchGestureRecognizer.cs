namespace Guinevere;

/// <summary>Optional deterministic recognition of raw contacts using a host's monotonic clock.</summary>
public sealed class TouchGestureRecognizer
{
    readonly Dictionary<long, ContactState> _contacts = new();
    (long First, long Second)? _pair;
    Vector2 _pairVector;
    /// <summary>Maximum travel for tap and long-press recognition in window pixels.</summary>
    public float Slop { get; set; } = 8;
    /// <summary>Maximum tap duration in seconds.</summary>
    public double TapSeconds { get; set; } = 0.3;
    /// <summary>Hold duration before a long press in seconds.</summary>
    public double LongPressSeconds { get; set; } = 0.5;
    /// <summary>Minimum release displacement for a swipe in window pixels.</summary>
    public float SwipeDistance { get; set; } = 30;

    /// <summary>Processes one frame's contact updates and emits gesture deltas.</summary>
    public IReadOnlyList<TouchGesture> Process(IReadOnlyList<TouchContact> updates, double elapsed)
    {
        var gestures = new List<TouchGesture>();
        foreach (var contact in updates)
        {
            UpdateContact(contact, elapsed, gestures);
        }

        foreach (var (id, state) in _contacts)
        {
            if (state.Multiple || state.LongPressed || state.Travel > Slop
                || elapsed - state.Started < LongPressSeconds)
            {
                continue;
            }

            state.LongPressed = true;
            gestures.Add(new TouchGesture(TouchGestureKind.LongPress, id, state.Position));
        }
        return gestures;
    }

    /// <summary>Forgets all contacts when an input source is reset or removed.</summary>
    public void Reset()
    {
        _contacts.Clear();
        _pair = null;
    }

    void UpdateContact(TouchContact contact, double elapsed, List<TouchGesture> gestures)
    {
        if (contact.Phase == TouchPhase.Began)
        {
            _contacts[contact.Id] = new ContactState(contact.Position, elapsed);
        }

        if (!_contacts.TryGetValue(contact.Id, out var state))
        {
            return;
        }

        state.Position = contact.Position;
        state.Travel = Math.Max(state.Travel, Vector2.Distance(state.Origin, contact.Position));
        if (contact.Phase is TouchPhase.Ended or TouchPhase.Canceled)
        {
            if (contact.Phase == TouchPhase.Ended)
            {
                RecognizeRelease(contact, state, elapsed, gestures);
            }

            _contacts.Remove(contact.Id);
        }
        RecognizePair(gestures);
    }

    void RecognizeRelease(TouchContact contact, ContactState state, double elapsed, List<TouchGesture> gestures)
    {
        if (state.Multiple || state.LongPressed)
        {
            return;
        }

        var delta = contact.Position - state.Origin;
        if (state.Travel <= Slop && elapsed - state.Started <= TapSeconds)
        {
            gestures.Add(new TouchGesture(TouchGestureKind.Tap, contact.Id, contact.Position));
        }
        else if (delta.Length() >= SwipeDistance)
        {
            gestures.Add(new TouchGesture(TouchGestureKind.Swipe, contact.Id, contact.Position, delta));
        }
    }

    void RecognizePair(List<TouchGesture> gestures)
    {
        if (!HasPair())
        {
            return;
        }

        var ids = _contacts.Keys.Order().ToArray();
        var first = _contacts[ids[0]];
        var second = _contacts[ids[1]];
        first.Multiple = second.Multiple = true;
        var vector = second.Position - first.Position;
        var pair = (ids[0], ids[1]);
        if (_pair == pair && _pairVector.Length() > 0 && vector.Length() > 0)
        {
            EmitPairGestures(ids[0], (first.Position + second.Position) / 2, vector, gestures);
        }

        _pair = pair;
        _pairVector = vector;
    }

    bool HasPair()
    {
        if (_contacts.Count != 2)
        {
            if (_contacts.Count > 1)
            {
                foreach (var state in _contacts.Values)
                {
                    state.Multiple = true;
                }
            }

            _pair = null;
            return false;
        }
        return true;
    }

    void EmitPairGestures(long id, Vector2 position, Vector2 vector, List<TouchGesture> gestures)
    {
        var scale = vector.Length() / _pairVector.Length();
        var rotation = MathF.Atan2(vector.Y, vector.X) - MathF.Atan2(_pairVector.Y, _pairVector.X);
        rotation = MathF.IEEERemainder(rotation, MathF.Tau);
        if (MathF.Abs(scale - 1) > 0.0001f)
        {
            gestures.Add(new TouchGesture(TouchGestureKind.Pinch, id, position, Scale: scale));
        }

        if (MathF.Abs(rotation) > 0.0001f)
        {
            gestures.Add(new TouchGesture(TouchGestureKind.Rotate, id, position, Rotation: rotation));
        }
    }

    sealed class ContactState(Vector2 origin, double started)
    {
        public Vector2 Origin { get; } = origin;
        public double Started { get; } = started;
        public Vector2 Position { get; set; } = origin;
        public float Travel { get; set; }
        public bool LongPressed { get; set; }
        public bool Multiple { get; set; }
    }
}
