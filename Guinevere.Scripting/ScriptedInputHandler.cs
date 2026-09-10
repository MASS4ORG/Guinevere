namespace Guinevere;

/// <summary>
/// An <see cref="IInputHandler"/> driven by code rather than a window, for tests and headless
/// automation. Call <see cref="NewFrame"/> once before each frame's build pass so that per-frame
/// edges — presses, releases, typed text, wheel movement — are visible to both passes and then clear.
/// </summary>
public sealed class ScriptedInputHandler : IInputHandler
{
    private readonly HashSet<MouseButton> _buttonsDown = [];
    private readonly HashSet<MouseButton> _buttonsPressed = [];
    private readonly HashSet<MouseButton> _buttonsReleased = [];
    private readonly HashSet<KeyboardKey> _keysDown = [];
    private readonly HashSet<KeyboardKey> _keysPressed = [];
    private readonly HashSet<KeyboardKey> _keysReleased = [];

    private string _typed = string.Empty;
    private string _clipboard = string.Empty;
    private Vector2 _position;
    private Vector2 _previous;
    private float _wheel;

    /// <inheritdoc />
    public Vector2 MousePosition => _position;

    /// <inheritdoc />
    public Vector2 PrevMousePosition => _previous;

    /// <inheritdoc />
    public Vector2 MouseDelta => _position - _previous;

    /// <inheritdoc />
    public float MouseWheelDelta => _wheel;

    /// <inheritdoc />
    public bool IsAnyKeyDown => _keysDown.Count > 0;

    /// <summary>Moves the pointer to a position in screen space.</summary>
    public void MoveTo(float x, float y) => MoveTo(new Vector2(x, y));

    /// <summary>Moves the pointer to a position in screen space.</summary>
    public void MoveTo(Vector2 position) => _position = position;

    /// <summary>Presses a mouse button and holds it until <see cref="ReleaseButton"/>.</summary>
    public void PressButton(MouseButton button = MouseButton.Left)
    {
        if (_buttonsDown.Add(button)) _buttonsPressed.Add(button);
    }

    /// <summary>Releases a held mouse button.</summary>
    public void ReleaseButton(MouseButton button = MouseButton.Left)
    {
        if (_buttonsDown.Remove(button)) _buttonsReleased.Add(button);
    }

    /// <summary>Presses a key and holds it until <see cref="ReleaseKey"/>.</summary>
    public void PressKey(KeyboardKey key)
    {
        if (_keysDown.Add(key)) _keysPressed.Add(key);
    }

    /// <summary>Releases a held key.</summary>
    public void ReleaseKey(KeyboardKey key)
    {
        if (_keysDown.Remove(key)) _keysReleased.Add(key);
    }

    /// <summary>Queues text for this frame's <see cref="GetTypedCharacters"/>.</summary>
    public void TypeText(string text) => _typed += text;

    /// <summary>Sets this frame's wheel movement.</summary>
    public void Scroll(float delta) => _wheel = delta;

    /// <summary>Releases every held button and key, and clears the pointer's movement history.</summary>
    public void Reset()
    {
        _buttonsDown.Clear();
        _keysDown.Clear();
        NewFrame();
        _previous = _position;
    }

    /// <summary>
    /// Rolls the per-frame state: edges from the frame just built are dropped and the pointer's
    /// previous position advances. Call once per frame, before the build pass.
    /// </summary>
    public void NewFrame()
    {
        _previous = _position;
        _buttonsPressed.Clear();
        _buttonsReleased.Clear();
        _keysPressed.Clear();
        _keysReleased.Clear();
        _typed = string.Empty;
        _wheel = 0f;
    }

    /// <inheritdoc />
    public bool IsMouseButtonPressed(MouseButton button) => _buttonsPressed.Contains(button);

    /// <inheritdoc />
    public bool IsMouseButtonDown(MouseButton button) => _buttonsDown.Contains(button);

    /// <inheritdoc />
    public bool IsMouseButtonUp(MouseButton button) => !_buttonsDown.Contains(button);

    /// <inheritdoc />
    public bool IsKeyPressed(KeyboardKey keyboardKey) => _keysPressed.Contains(keyboardKey);

    /// <inheritdoc />
    public bool IsKeyDown(KeyboardKey keyboardKey) => _keysDown.Contains(keyboardKey);

    /// <inheritdoc />
    public bool IsKeyUp(KeyboardKey keyboardKey) => !_keysDown.Contains(keyboardKey);

    /// <inheritdoc />
    public string GetTypedCharacters() => _typed;

    /// <inheritdoc />
    public string GetClipboardText() => _clipboard;

    /// <inheritdoc />
    public void SetClipboardText(string text) => _clipboard = text;
}
