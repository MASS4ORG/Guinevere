namespace Guinevere;

/// <summary>
/// The input seen by polling code for one frame: the platform handler, minus the edges an event listener
/// handled with <see cref="GuiEvent.PreventDefault"/>. Typed text is read from the platform once per frame,
/// so event dispatch and a text input can both see it.
/// </summary>
sealed class FrameInput(IInputHandler source) : IInputHandler
{
    readonly HashSet<KeyboardKey> _suppressedKeys = [];
    int _suppressedButtons;
    bool _suppressWheel;
    string? _text;
    bool _textConsumed;
    bool _suppressText;

    public IInputHandler Source { get; } = source;

    /// <summary>Starts a frame: forgets suppressed edges and the previous frame's text.</summary>
    public void BeginFrame()
    {
        _suppressedKeys.Clear();
        _suppressedButtons = 0;
        _suppressWheel = false;
        _text = null;
        _textConsumed = false;
        _suppressText = false;
    }

    /// <summary>This frame's typed text, read from the platform at most once.</summary>
    public string PeekText() => _text ??= Source.GetTypedCharacters();

    public void SuppressPress(MouseButton button) => _suppressedButtons |= 1 << (int)button;
    public void SuppressWheel() => _suppressWheel = true;
    public void SuppressKeyPress(KeyboardKey key) => _suppressedKeys.Add(key);
    public void SuppressText() => _suppressText = true;

    public bool IsAnyKeyDown => Source.IsAnyKeyDown;
    public Vector2 MouseDelta => Source.MouseDelta;
    public Vector2 MousePosition => Source.MousePosition;
    public float MouseWheelDelta => _suppressWheel ? 0f : Source.MouseWheelDelta;
    public Vector2 PrevMousePosition => Source.PrevMousePosition;

    public bool IsKeyPressed(KeyboardKey keyboardKey) =>
        !_suppressedKeys.Contains(keyboardKey) && Source.IsKeyPressed(keyboardKey);

    public bool IsKeyDown(KeyboardKey keyboardKey) => Source.IsKeyDown(keyboardKey);
    public bool IsKeyUp(KeyboardKey keyboardKey) => Source.IsKeyUp(keyboardKey);

    public bool IsMouseButtonPressed(MouseButton button) =>
        (_suppressedButtons & (1 << (int)button)) == 0 && Source.IsMouseButtonPressed(button);

    public bool IsMouseButtonDown(MouseButton button) => Source.IsMouseButtonDown(button);
    public bool IsMouseButtonUp(MouseButton button) => Source.IsMouseButtonUp(button);

    /// <summary>Hands this frame's text out once, as the platform handlers do.</summary>
    public string GetTypedCharacters()
    {
        var text = PeekText();
        if (_suppressText || _textConsumed) return string.Empty;
        _textConsumed = true;
        return text;
    }

    public string GetClipboardText() => Source.GetClipboardText();
    public void SetClipboardText(string text) => Source.SetClipboardText(text);
}
