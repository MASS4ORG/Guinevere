using Silk.NET.Windowing;

namespace Guinevere;

public partial class GuiWindow
{
    /// <summary>
    /// When frames render: on demand by default; clear <see cref="FramePacer.OnDemand"/> to render continuously.
    /// <c>GUINEVERE_PACING=on-demand</c> or <c>continuous</c> overrides it at run time.
    /// </summary>
    public FramePacer Pacing { get; } = new();

    /// <summary>
    /// Runs the GUI application with the specified draw callback until the window closes.
    /// </summary>
    /// <param name="draw">The callback method that defines the GUI layout and rendering.</param>
    public void RunGui(Action draw)
    {
        _draw = draw;
        Pacing.ApplyOverride(Environment.GetEnvironmentVariable("GUINEVERE_PACING"));
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

    WindowState? _windowState;

    /// <summary>
    /// The window state, kept from state-change events: querying it asks the window system, which on X11 is a
    /// server round-trip, and chrome controls read it several times a frame.
    /// </summary>
    WindowState CurrentWindowState => _windowState ??= _window.WindowState;

    /// <summary>Window events that change what is shown count as input for pacing.</summary>
    void HookPacingEvents()
    {
        _window.FocusChanged += _ => Pacing.NotifyInput();
        _window.StateChanged += state =>
        {
            _windowState = state;
            Pacing.NotifyInput();
        };
    }

    /// <summary>Waits for input when nothing needs a frame, then processes events and renders one frame.</summary>
    void RunFrame()
    {
        PollGameInput();
        GameInputState.WaitForInput(InputWaitSeconds, _glfw.WaitEventsTimeout, PollGameInput);
        _window.DoEvents();
        UpdateOpenFrame();
    }

    bool HasControllerInput => _gameInput.AnyButtonDown || _gui.ControllerNavigation.HasPendingRepeat;
    bool HasHeldInput => _heldButtons.Count + _heldKeys.Count > 0 || HasControllerInput;
    double InputWaitSeconds() => _window.IsClosing ? 0 : Pacing.WaitSeconds(_gui, HasHeldInput);

    void UpdateOpenFrame()
    {
        if (_window.IsClosing) return;
        _window.DoUpdate();
        RenderOpenFrame();
    }

    void RenderOpenFrame()
    {
        if (_window.IsClosing) return;
        _window.DoRender();
        _gameInput.NewFrame();
        Pacing.FrameRendered();
    }
}
