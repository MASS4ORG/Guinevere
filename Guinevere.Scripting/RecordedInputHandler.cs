namespace Guinevere;

/// <summary>Delivers one immutable recorded frame while forwarding clipboard operations to its host.</summary>
public sealed class RecordedInputHandler(IClipboard clipboard) : IInputHandler, IGameInputCapability, ITouchCapability
{
    RecordedInputFrame? _frame;
    bool _textConsumed;

    /// <summary>Installs a frame; prior values remain owned by their recording.</summary>
    public void SetFrame(RecordedInputFrame frame)
    {
        _frame = frame;
        _textConsumed = false;
    }

    /// <inheritdoc />
    public bool IsAnyKeyDown => _frame?.HeldKeys.Count > 0;
    /// <inheritdoc />
    public Vector2 MousePosition => _frame?.Position ?? default;
    /// <inheritdoc />
    public Vector2 PrevMousePosition => _frame?.Previous ?? default;
    /// <inheritdoc />
    public Vector2 MouseDelta => MousePosition - PrevMousePosition;
    /// <inheritdoc />
    public float MouseWheelDelta => _frame?.Wheel ?? 0;
    /// <inheritdoc />
    public bool IsKeyDown(KeyboardKey keyboardKey) => _frame?.HeldKeys.Contains(keyboardKey) == true;
    /// <inheritdoc />
    public bool IsKeyPressed(KeyboardKey keyboardKey) => _frame?.PressedKeys.Contains(keyboardKey) == true;
    /// <inheritdoc />
    public bool IsKeyUp(KeyboardKey keyboardKey) => !IsKeyDown(keyboardKey);
    /// <inheritdoc />
    public bool IsMouseButtonDown(MouseButton button) => _frame?.HeldButtons.Contains(button) == true;
    /// <inheritdoc />
    public bool IsMouseButtonPressed(MouseButton button) => _frame?.PressedButtons.Contains(button) == true;
    /// <inheritdoc />
    public bool IsMouseButtonUp(MouseButton button) => !IsMouseButtonDown(button);
    /// <inheritdoc />
    public string GetTypedCharacters()
    {
        var result = _textConsumed ? "" : _frame?.Text ?? "";
        _textConsumed = true;
        return result;
    }
    /// <inheritdoc />
    public string GetClipboardText() => clipboard.GetClipboardText();
    /// <inheritdoc />
    public void SetClipboardText(string text) => clipboard.SetClipboardText(text);
    /// <inheritdoc />
    public IReadOnlyList<GameInputDevice> Devices => _frame?.Devices ?? (IReadOnlyList<GameInputDevice>)[];
    /// <inheritdoc />
    public IReadOnlyList<GameDeviceConnection> Connections =>
        _frame?.Connections ?? (IReadOnlyList<GameDeviceConnection>)[];
    /// <inheritdoc />
    public IReadOnlyList<TouchContact> Contacts => _frame?.Contacts ?? (IReadOnlyList<TouchContact>)[];
    /// <inheritdoc />
    public IReadOnlyList<TouchGesture> Gestures => _frame?.Gestures ?? (IReadOnlyList<TouchGesture>)[];
}
