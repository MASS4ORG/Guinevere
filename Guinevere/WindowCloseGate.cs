namespace Guinevere;

/// <summary>
/// Arbitrates a window close request against a handler that may veto it. Integrations share this because every
/// windowing library also raises its closing event from its own close call, which means a handler is free to answer
/// by closing instead of returning true.
/// </summary>
public sealed class WindowCloseGate
{
    bool _approved;

    /// <summary>
    /// Asked when the user closes the window, such as with its close button. Returning false keeps the window open, so
    /// an application can first ask about unsaved work and close later with <see cref="Approve"/>.
    /// </summary>
    public Func<bool>? CloseRequested { get; set; }

    /// <summary>Whether a close has been granted and the run loop should stop.</summary>
    public bool Approved => _approved;

    /// <summary>
    /// Grants the close, as an application asks for when it calls its own close. A close is never vetoed, and this is
    /// safe to call from inside <see cref="CloseRequested"/>: the latch is read again afterwards, so the handler's
    /// return value cannot take the exit back.
    /// </summary>
    public void Approve()
    {
        _approved = true;
    }

    /// <summary>
    /// Whether a close request may go ahead. Grants the close when there is no handler, when the handler allows it, or
    /// when the handler approved it from inside by calling <see cref="Approve"/>.
    /// </summary>
    public bool MayClose()
    {
        if (_approved) return true;
        if (CloseRequested?.Invoke() != false) return true;
        return _approved;
    }
}
