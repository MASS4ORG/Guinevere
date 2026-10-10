using System.Diagnostics;

namespace Guinevere;

public partial class Gui
{
    int _frameRequested;
    long _frameDeadline = long.MaxValue;
    int _frameThread;

    /// <summary>
    /// Wakes a host that is waiting for input when a frame is requested from another thread. Window integrations set
    /// it; it must be safe to call from any thread.
    /// </summary>
    public Action? WakeHost { get; set; }

    /// <summary>
    /// Asks the host for another frame as soon as its pacing allows, for state that changes without input. Safe to
    /// call from any thread; keyed animations, style transitions and built-in timed controls call it themselves.
    /// </summary>
    public void RequestFrame()
    {
        Volatile.Write(ref _frameRequested, 1);
        WakeFromOtherThread();
    }

    /// <summary>
    /// Asks the host for a frame no later than <paramref name="seconds"/> from now, for delays and timers. The
    /// earliest pending request wins. Safe to call from any thread.
    /// </summary>
    /// <param name="seconds">Delay before the frame; zero or less requests one immediately.</param>
    public void RequestFrameIn(double seconds)
    {
        if (!(seconds > 0))
        {
            RequestFrame();
            return;
        }
        var deadline = Stopwatch.GetTimestamp() + (long)(Math.Min(seconds, 3600d) * Stopwatch.Frequency);
        long current;
        do
        {
            current = Volatile.Read(ref _frameDeadline);
            if (deadline >= current) return;
        } while (Interlocked.CompareExchange(ref _frameDeadline, deadline, current) != current);
        WakeFromOtherThread();
    }

    /// <summary>
    /// Seconds a host may wait for input before rendering again: zero when a frame was requested, the time left
    /// until the earliest timed request, or positive infinity when nothing is pending.
    /// </summary>
    public double FrameWaitSeconds
    {
        get
        {
            if (Volatile.Read(ref _frameRequested) != 0) return 0;
            var deadline = Volatile.Read(ref _frameDeadline);
            if (deadline == long.MaxValue) return double.PositiveInfinity;
            return Math.Max(0, (deadline - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency);
        }
    }

    /// <summary>Requests made before a frame starts are served by it; later ones ask for the next frame.</summary>
    void BeginFrameRequests()
    {
        _frameThread = Environment.CurrentManagedThreadId;
        Volatile.Write(ref _frameRequested, 0);
        var deadline = Volatile.Read(ref _frameDeadline);
        if (deadline <= Stopwatch.GetTimestamp())
            Interlocked.CompareExchange(ref _frameDeadline, long.MaxValue, deadline);
    }

    void WakeFromOtherThread()
    {
        if (Environment.CurrentManagedThreadId != _frameThread) WakeHost?.Invoke();
    }
}
