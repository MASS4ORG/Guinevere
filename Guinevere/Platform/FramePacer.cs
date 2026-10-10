using System.Diagnostics;

namespace Guinevere;

/// <summary>
/// Decides when a windowed host renders. On demand by default: the host waits for input between frames and renders
/// only after input, while input is held, when the GUI requests a frame, and at least every
/// <see cref="MaxIdleSeconds"/>. Clearing <see cref="OnDemand"/> renders continuously.
/// </summary>
public sealed class FramePacer
{
    int _settleFrames;
    long _lastFrame = Stopwatch.GetTimestamp();

    /// <summary>
    /// Whether the host waits for input or a frame request instead of rendering continuously. True by default;
    /// content that changes without input calls <see cref="Gui.RequestFrame"/>.
    /// </summary>
    public bool OnDemand { get; set; } = true;

    /// <summary>
    /// Frames rendered after each input event: events dispatch one frame after they arrive and their effects show
    /// one frame later.
    /// </summary>
    public int SettleFrames { get; set; } = 3;

    /// <summary>The longest wait between frames, which keeps time-driven UI that never requests frames alive.</summary>
    public double MaxIdleSeconds { get; set; } = 0.5;

    /// <summary>
    /// Applies a run-time override of <see cref="OnDemand"/>: <c>GUINEVERE_PACING=on-demand</c> or <c>continuous</c>.
    /// Window integrations call it when their run loop starts.
    /// </summary>
    /// <param name="value">The variable's value; null or anything else keeps the current setting.</param>
    public void ApplyOverride(string? value)
    {
        if (value == "on-demand") OnDemand = true;
        else if (value == "continuous") OnDemand = false;
    }

    /// <summary>Records an input or window event, which renders the next <see cref="SettleFrames"/> frames.</summary>
    public void NotifyInput() => _settleFrames = SettleFrames;

    /// <summary>
    /// Seconds to keep waiting for events before the next frame; zero renders it now. Hosts call it again after every
    /// wake-up, since platform events that are not input must not render a frame on their own.
    /// </summary>
    /// <param name="gui">The GUI whose frame requests are honored.</param>
    /// <param name="inputHeld">Whether a pointer button or key is held, which renders continuously.</param>
    public double WaitSeconds(Gui gui, bool inputHeld)
    {
        ArgumentNullException.ThrowIfNull(gui);
        if (!OnDemand || inputHeld || _settleFrames > 0) return 0;
        var idle = MaxIdleSeconds - Stopwatch.GetElapsedTime(_lastFrame).TotalSeconds;
        return Math.Max(0, Math.Min(gui.FrameWaitSeconds, idle));
    }

    /// <summary>Records a rendered frame and counts it against the pending settle frames.</summary>
    public void FrameRendered()
    {
        _lastFrame = Stopwatch.GetTimestamp();
        if (_settleFrames > 0) _settleFrames--;
    }
}
