namespace Guinevere;

/// <summary>
/// What every integration's window needs to shut down safely after its run loop threw. Windowing libraries stay
/// "inside the render loop" when an exception leaves their run call, so tearing the native window down from the
/// caller's <c>using</c> throws again and that second exception replaces the one that mattered. A window records
/// the fault here, rethrows the original exception unchanged, and disposes through <see cref="Teardown"/>, which
/// never throws.
/// </summary>
public sealed class WindowLifecycle
{
    /// <summary>Whether the run loop ended because something threw.</summary>
    public bool Faulted { get; private set; }

    /// <summary>Whether disposal has started; a second dispose does nothing.</summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// Runs the loop. When it throws, the fault is recorded and the same exception continues up, with its type,
    /// message and stack trace untouched.
    /// </summary>
    /// <param name="loop">The window's run loop.</param>
    public void Run(Action loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        try
        {
            loop();
        }
        catch
        {
            Faulted = true;
            throw;
        }
    }

    /// <summary>
    /// Claims the disposal. Returns true for the first caller only, so a window disposed twice (a <c>using</c> and an
    /// explicit call) releases its resources once.
    /// </summary>
    public bool BeginDispose()
    {
        if (Disposed)
        {
            return false;
        }

        Disposed = true;
        return true;
    }

    /// <summary>
    /// Runs one teardown step so that nothing it throws escapes: the failure is handed to <paramref name="report"/>
    /// and the next step still runs. After a faulted loop a native reset is expected to throw, and that is reported
    /// as the consequence of the fault rather than as a new failure.
    /// </summary>
    /// <param name="step">What is being released, for the report.</param>
    /// <param name="action">The release itself.</param>
    /// <param name="report">Receives the step and the suppressed exception; null writes to standard error.</param>
    public void Teardown(string step, Action action, Action<string, Exception>? report = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
        }
        catch (Exception exception)
        {
            var reason = Faulted ? "after the run loop faulted" : "during disposal";
            if (report is not null)
            {
                report($"Suppressed while releasing {step} {reason}", exception);
            }
            else
            {
                Console.Error.WriteLine($"Suppressed while releasing {step} {reason}: {exception.GetType().Name}: {exception.Message}");
            }
        }
    }
}
