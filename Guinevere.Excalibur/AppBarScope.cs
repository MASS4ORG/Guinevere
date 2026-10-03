namespace Guinevere;

/// <summary>An application bar scope that appends native window controls after the caller's content.</summary>
public readonly struct AppBarScope : IDisposable
{
    readonly Gui _gui;
    readonly LayoutNodeScope _bar;
    readonly LayoutNodeScope _content;
    readonly ControlsExtensions.AppBarState _state;
    readonly IWindowChromeCapability? _window;
    readonly float _height;

    internal AppBarScope(Gui gui, LayoutNodeScope bar, LayoutNodeScope content,
        ControlsExtensions.AppBarState state, IWindowChromeCapability? window, float height)
    {
        _gui = gui;
        _bar = bar;
        _content = content;
        _state = state;
        _window = window;
        _height = height;
    }

    /// <summary>The content row, for ordinary layout styling such as gaps and padding.</summary>
    public LayoutNode Node => _content.Node;

    /// <summary>Finishes window interaction and controls, then exits the application bar.</summary>
    public void Dispose() => ControlsExtensions.EndAppBar(_gui, _state, _window, _content, _bar, _height);
}
