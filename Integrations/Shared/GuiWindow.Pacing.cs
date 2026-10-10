namespace Guinevere;

public partial class GuiWindow
{
    /// <summary>
    /// When frames render. Set <see cref="FramePacer.OnDemand"/> to wait for input between frames instead of
    /// rendering continuously; <c>GUINEVERE_PACING=on-demand</c> or <c>continuous</c> overrides it at run time.
    /// </summary>
    public FramePacer Pacing { get; } = new();

    /// <summary>
    /// Runs the GUI application with the specified draw callback until the window closes.
    /// </summary>
    /// <param name="draw">The callback method that defines the GUI layout and rendering.</param>
    public void RunGui(Action draw)
    {
        _draw = draw;
        switch (Environment.GetEnvironmentVariable("GUINEVERE_PACING"))
        {
            case "on-demand": Pacing.OnDemand = true; break;
            case "continuous": Pacing.OnDemand = false; break;
        }
        _gui.WakeHost = _glfw.PostEmptyEvent;
        try
        {
            if (!_window.IsInitialized) _window.Initialize();
            _window.Run(RunFrame);
            _window.DoEvents();
            _window.Reset();
        }
        finally
        {
            _gui.WakeHost = null;
        }
    }

    /// <summary>Window events that change what is shown count as input for pacing.</summary>
    void HookPacingEvents()
    {
        _window.FocusChanged += _ => Pacing.NotifyInput();
        _window.StateChanged += _ => Pacing.NotifyInput();
    }

    /// <summary>Waits for input when nothing needs a frame, then processes events and renders one frame.</summary>
    void RunFrame()
    {
        double wait;
        while (!_window.IsClosing && (wait = Pacing.WaitSeconds(_gui, _heldButtons.Count > 0 || _heldKeys.Count > 0)) > 0)
            _glfw.WaitEventsTimeout(wait);
        _window.DoEvents();
        if (_window.IsClosing) return;
        _window.DoUpdate();
        if (_window.IsClosing) return;
        _window.DoRender();
        Pacing.FrameRendered();
    }
}
