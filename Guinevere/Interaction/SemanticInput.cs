namespace Guinevere;

/// <summary>Application actions independent of physical button layouts.</summary>
public enum SemanticAction
{
    /// <summary>Activate the focused control.</summary>
    Accept,
    /// <summary>Dismiss the current menu or dialog.</summary>
    Cancel,
    /// <summary>Open contextual commands.</summary>
    Context,
    /// <summary>Select the previous tab.</summary>
    PreviousTab,
    /// <summary>Select the next tab.</summary>
    NextTab,
    /// <summary>Navigate left.</summary>
    Left,
    /// <summary>Navigate right.</summary>
    Right,
    /// <summary>Navigate up.</summary>
    Up,
    /// <summary>Navigate down.</summary>
    Down
}

/// <summary>Device families used to select application-owned input prompts.</summary>
public enum InputDeviceKind
{
    /// <summary>Keyboard or mouse input.</summary>
    KeyboardMouse,
    /// <summary>Controller or joystick input.</summary>
    Controller,
    /// <summary>Touch or pen input.</summary>
    Touch
}

/// <summary>A device identity supplied by an input adapter.</summary>
public readonly record struct InputDevice(string Id, InputDeviceKind Kind);

/// <summary>Optional semantic input supplied by the application or a platform adapter.</summary>
public interface ISemanticInputCapability : IPlatformCapability
{
    /// <summary>The device that most recently supplied meaningful input.</summary>
    InputDevice ActiveDevice { get; }
    /// <summary>Returns the action edge for the current frame, including navigation repeats.</summary>
    bool IsPressed(SemanticAction action);
}

/// <summary>A prompt whose glyph asset remains owned by the provider.</summary>
public sealed record InputPrompt(string Label, IDrawable? Glyph = null);

/// <summary>Resolves prompts at runtime so device and mapping changes are immediately visible.</summary>
public interface IInputPromptProvider : IPlatformCapability
{
    /// <summary>Returns the prompt for an action on the active device, or null if unavailable.</summary>
    InputPrompt? GetPrompt(SemanticAction action, InputDevice device);
}

/// <summary>Optional host bridge for a virtual cursor driven by a controller, touch, or virtual stick.</summary>
public interface IVirtualPointerCapability : IPlatformCapability
{
    /// <summary>Applies window-space motion and primary button state to the host's pointer input.</summary>
    void UpdatePointer(Vector2 motion, bool primaryDown);
}
