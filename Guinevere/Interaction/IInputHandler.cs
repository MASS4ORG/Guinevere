namespace Guinevere;

/// <summary>
/// Provides input handling functionalities including keyboard and mouse events.
/// </summary>
public interface IInputHandler : IClipboard, IPlatformCapability
{
    /// <summary>
    /// Gets a value indicating whether any key on the keyboard is currently being pressed.
    /// This property is useful for detecting general keyboard activity without targeting
    /// specific keys. The implementation may vary depending on the input system being used.
    /// </summary>
    bool IsAnyKeyDown { get; }

    /// <summary>
    /// Gets the change in the mouse pointer's position since the previous frame.
    /// The delta is represented as a vector, where the X and Y components
    /// correspond to the horizontal and vertical differences in mouse movement, respectively.
    /// This is useful for tracking relative mouse movement over time.
    /// </summary>

    Vector2 MouseDelta { get; }

    /// <summary>
    /// Gets the current position of the mouse pointer in screen coordinates.
    /// The position is represented as a vector, where the X and Y components
    /// correspond to the horizontal and vertical mouse location respectively.
    /// This can be used to track mouse movement or interaction within the application.
    /// </summary>
    Vector2 MousePosition { get; }

    /// <summary>
    /// Gets the change in the mouse wheel's scrolling value since the last frame.
    /// This value represents the amount of scrolling performed, which can be positive for scrolling up
    /// or negative for scrolling down, depending on the platform and input device configuration.
    /// </summary>
    float MouseWheelDelta { get; }

    /// <summary>
    /// Gets the previous position of the mouse cursor in screen coordinates.
    /// This represents the mouse position in the previous frame, which can be used
    /// to calculate movement or interactions based on changes in position.
    /// </summary>
    /// <remarks>
    /// Integrations differ on what "previous" means: most update this from the pointer-move event, so
    /// once the pointer stops it keeps reporting the position before the last movement. For per-frame
    /// movement use <see cref="DragArgs.FrameDelta"/>, which <see cref="Gui"/> tracks itself.
    /// </remarks>
    Vector2 PrevMousePosition { get; }

    /// <summary>
    /// Checks if the specified keyboard key was pressed on the current frame.
    /// </summary>
    /// <param name="keyboardKey">The keyboard key to check for a press event.</param>
    /// <returns>True if the specified key was pressed in the current frame; otherwise, false.</returns>
    bool IsKeyPressed(KeyboardKey keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently pressed.
    /// </summary>
    /// <param name="keyboardKey">The keyboard key to check.</param>
    /// <returns>True if the specified key is pressed; otherwise, false.</returns>
    bool IsKeyDown(KeyboardKey keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently not pressed or released.
    /// </summary>
    /// <param name="keyboardKey">The keyboard key to check.</param>
    /// <returns>True if the specified key is not pressed or has been released; otherwise, false.</returns>
    bool IsKeyUp(KeyboardKey keyboardKey);

    /// <summary>
    /// Checks whether the specified mouse button is currently pressed.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the specified mouse button is pressed; otherwise, false.</returns>
    bool IsMouseButtonPressed(MouseButton button);

    /// <summary>
    /// Determines whether the specified mouse button is currently in the "down" state (pressed).
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the specified mouse button is down; otherwise, false.</returns>
    bool IsMouseButtonDown(MouseButton button);

    /// <summary>
    /// Determines whether the specified mouse button is currently in the "up" state (not pressed).
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the specified mouse button is up; otherwise, false.</returns>
    bool IsMouseButtonUp(MouseButton button);

    /// <summary>
    /// Retrieves characters that have been typed by the user during the current frame or input cycle.
    /// </summary>
    /// <returns>A string containing the characters typed by the user. Returns an empty string if no characters were typed.</returns>
    string GetTypedCharacters();

}
