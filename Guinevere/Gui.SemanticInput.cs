namespace Guinevere;

public partial class Gui
{
    readonly HashSet<SemanticAction> _semanticActions = [];
    InputDevice _activeInputDevice = new("keyboard-mouse", InputDeviceKind.KeyboardMouse);
    InputDevice? _lastSemanticDevice;
    Vector2 _promptPointerPosition;

    /// <summary>Default controller mappings and repeat settings, used when no semantic source is registered.</summary>
    public GamepadNavigation ControllerNavigation { get; } = new();

    /// <summary>The device currently used to resolve input prompts.</summary>
    public InputDevice ActiveInputDevice => _activeInputDevice;

    /// <summary>Returns a semantic action sampled once for both frame passes.</summary>
    public bool IsSemanticActionPressed(SemanticAction action) => _semanticActions.Contains(action);

    /// <summary>Whether keyboard or semantic input requests activation of a focused control.</summary>
    public bool IsAcceptPressed => Input.IsKeyPressed(KeyboardKey.Enter) || Input.IsKeyPressed(KeyboardKey.Space)
        || IsSemanticActionPressed(SemanticAction.Accept);

    /// <summary>Whether keyboard or semantic input requests dismissal of the active surface.</summary>
    public bool IsCancelPressed => Input.IsKeyPressed(KeyboardKey.Escape)
        || IsSemanticActionPressed(SemanticAction.Cancel);

    /// <summary>Reads a directional keyboard or semantic action for controls that own arrow navigation.</summary>
    public bool IsNavigationPressed(FocusDirection direction) => direction switch
    {
        FocusDirection.Left => Input.IsKeyPressed(KeyboardKey.Left) || IsSemanticActionPressed(SemanticAction.Left),
        FocusDirection.Right => Input.IsKeyPressed(KeyboardKey.Right) || IsSemanticActionPressed(SemanticAction.Right),
        FocusDirection.Up => Input.IsKeyPressed(KeyboardKey.Up) || IsSemanticActionPressed(SemanticAction.Up),
        FocusDirection.Down => Input.IsKeyPressed(KeyboardKey.Down) || IsSemanticActionPressed(SemanticAction.Down),
        _ => false
    };

    /// <summary>Resolves the current provider-owned prompt without caching its asset.</summary>
    public InputPrompt? GetInputPrompt(SemanticAction action) =>
        Platform.TryGet<IInputPromptProvider>(out var provider) ? provider?.GetPrompt(action, ActiveInputDevice) : null;

    void BeginSemanticInput()
    {
        _semanticActions.Clear();
        var source = ResolveSemanticInput();
        if (source is not null)
            SampleSemanticSource(source);
        else if (_lastSemanticDevice is not null)
        {
            _activeInputDevice = new InputDevice("keyboard-mouse", InputDeviceKind.KeyboardMouse);
            _lastSemanticDevice = null;
        }

        if (HasKeyboardPointerActivity())
            _activeInputDevice = new InputDevice("keyboard-mouse", InputDeviceKind.KeyboardMouse);
        UpdateTouchInputDevice();
    }

    void SampleSemanticSource(ISemanticInputCapability source)
    {
        foreach (var action in Enum.GetValues<SemanticAction>())
            if (source.IsPressed(action)) _semanticActions.Add(action);
        if (_semanticActions.Count > 0 || _lastSemanticDevice != source.ActiveDevice)
            _activeInputDevice = source.ActiveDevice;
        _lastSemanticDevice = source.ActiveDevice;
    }

    void UpdateTouchInputDevice()
    {
        if (Platform.TryGet<ITouchCapability>(out var touch) && touch is not null
            && touch.Contacts.Any(contact => contact.Phase is TouchPhase.Began or TouchPhase.Moved))
            _activeInputDevice = new InputDevice("touch", InputDeviceKind.Touch);
    }

    ISemanticInputCapability? ResolveSemanticInput()
    {
        if (Platform.TryGet<ISemanticInputCapability>(out var source)) return source;
        if (!Platform.TryGet<IGameInputCapability>(out var game) || game is null) return null;
        ControllerNavigation.Update(game, Clock.Elapsed);
        return ControllerNavigation;
    }

    bool HasKeyboardPointerActivity()
    {
        if (!HasInput) return false;
        var input = RawInput;
        var moved = input.MousePosition != _promptPointerPosition;
        _promptPointerPosition = input.MousePosition;
        return moved || input.MouseWheelDelta != 0 || AllButtons.Any(input.IsMouseButtonPressed)
            || AllKeys.Any(input.IsKeyPressed);
    }

    void NavigateSemanticInput()
    {
        if (!Focus.CanNavigateDirectionally) return;
        if (IsSemanticActionPressed(SemanticAction.Left)) Focus.Navigate(FocusDirection.Left);
        else if (IsSemanticActionPressed(SemanticAction.Right)) Focus.Navigate(FocusDirection.Right);
        else if (IsSemanticActionPressed(SemanticAction.Up)) Focus.Navigate(FocusDirection.Up);
        else if (IsSemanticActionPressed(SemanticAction.Down)) Focus.Navigate(FocusDirection.Down);
    }
}
