namespace Guinevere;

public partial class Gui
{
    readonly Dictionary<long, string> _touchCaptures = new();
    readonly Dictionary<long, TouchContact> _activeTouches = new();
    ITouchCapability? _lastTouchSource;
    /// <summary>Optional raw-contact recognition; null uses only host-provided gestures.</summary>
    public TouchGestureRecognizer? GestureRecognizer { get; set; }
    /// <summary>Returns the node capturing a contact, or null if uncaptured.</summary>
    public string? TouchCapture(long contactId) => _touchCaptures.GetValueOrDefault(contactId);

    void DispatchTouchEvents()
    {
        Platform.TryGet<ITouchCapability>(out var source);
        if (_lastTouchSource != source)
        {
            CancelActiveTouches();
            GestureRecognizer?.Reset();
            _lastTouchSource = source;
        }
        if (source is null || RootNode is not { } root)
        {
            return;
        }

        DispatchGestures(root, source, RecognizableContacts(root, source.Contacts));
        ReleaseEndedContacts(source.Contacts);
        if (GestureRecognizer is not null && _activeTouches.Count > 0)
        {
            RequestFrameIn(1f / 60);
        }
    }

    List<TouchContact> RecognizableContacts(LayoutNode root, IReadOnlyList<TouchContact> contacts)
    {
        var recognizable = new List<TouchContact>();
        foreach (var contact in contacts)
        {
            var prevented = DispatchContact(root, contact);
            recognizable.Add(prevented ? contact with { Phase = TouchPhase.Canceled } : contact);
        }
        return recognizable;
    }

    bool DispatchContact(LayoutNode root, TouchContact contact)
    {
        var ended = contact.Phase is TouchPhase.Ended or TouchPhase.Canceled;
        if (!ended)
        {
            _activeTouches[contact.Id] = contact;
        }

        var target = TouchTarget(root, contact.Id, contact.Position);
        if (target is null)
        {
            return false;
        }

        var e = Dispatch(new TouchEvent { Contact = contact, Position = contact.Position }, target);
        if (!ended && e.CaptureTarget is { } owner)
        {
            _touchCaptures[contact.Id] = owner;
        }

        return e.DefaultPrevented;
    }

    void DispatchGestures(LayoutNode root, ITouchCapability source, IReadOnlyList<TouchContact> recognizable)
    {
        var recognized = GestureRecognizer?.Process(recognizable, Clock.Elapsed) ?? [];
        foreach (var gesture in source.Gestures.Concat(recognized))
        {
            if (TouchTarget(root, gesture.ContactId, gesture.Position) is { } target)
            {
                Dispatch(new TouchGestureEvent { Gesture = gesture }, target);
            }
        }
    }

    void ReleaseEndedContacts(IReadOnlyList<TouchContact> contacts)
    {
        foreach (var contact in contacts)
        {
            if (contact.Phase is TouchPhase.Ended or TouchPhase.Canceled)
            {
                _activeTouches.Remove(contact.Id);
                _touchCaptures.Remove(contact.Id);
            }
        }
    }

    LayoutNode? TouchTarget(LayoutNode root, long id, Vector2 position) =>
        _touchCaptures.TryGetValue(id, out var owner) ? FindNode(root, owner) : HitTest(root, position);

    void CancelActiveTouches()
    {
        if (RootNode is { } root)
        {
            foreach (var (id, contact) in _activeTouches)
            {
                if (TouchTarget(root, id, contact.Position) is { } target)
                {
                    Dispatch(new TouchEvent
                    {
                        Contact = contact with { Phase = TouchPhase.Canceled },
                        Position = contact.Position
                    }, target);
                }
            }
        }

        _activeTouches.Clear();
        _touchCaptures.Clear();
    }

    void ReleaseRemovedTouchCaptures()
    {
        if (RootNode is not { } root)
        {
            return;
        }

        foreach (var (id, owner) in _touchCaptures.ToArray())
        {
            if (FindNode(root, owner) is null)
            {
                _touchCaptures.Remove(id);
                _activeTouches.Remove(id);
            }
        }
    }
}
