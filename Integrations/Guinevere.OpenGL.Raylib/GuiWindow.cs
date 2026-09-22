using System.Numerics;
using System.Reflection;
using System.Text;
using Raylib_cs;

namespace Guinevere;

/// <summary>
/// Represents a GUI window implementation using Raylib for OpenGL rendering.
/// Provides input handling, window management, and rendering capabilities for the Guinevere GUI framework.
/// </summary>
public class GuiWindow : IDisposable, IInputHandler, IWindowHandler, IDisplayCapability
{
    readonly ICanvasRenderer _canvasRenderer;
    readonly Gui _gui;
    int _width;
    int _height;
    readonly StringBuilder _typedCharacters = new();
    Vector2 _prevMousePosition;
    Vector2 _currentMousePosition;
    readonly Font _fontText;
    readonly Font _fontIcon;

    /// <summary>
    /// Initializes a new instance of the GuiWindow class with the specified parameters.
    /// </summary>
    /// <param name="gui">The GUI instance to render.</param>
    /// <param name="width">The initial width of the window. Default is 800.</param>
    /// <param name="height">The initial height of the window. Default is 600.</param>
    /// <param name="title">The title of the window. Default is empty string.</param>
    public GuiWindow(Gui gui, int width = 800, int height = 600, string title = "")
    {
        _width = width;
        _height = height;
        _gui = gui;
        _gui.Input = this;
        _gui.WindowHandler = this;
        _gui.Platform.Register<IDisplayCapability>(this);
        var fontStream = GetStreamResource("Guinevere.font.ttf");
        _fontText = Font.FromStream(fontStream);
        fontStream = GetStreamResource("Guinevere.icons.ttf");
        _fontIcon = Font.FromStream(fontStream);
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning); // Reduce verbose logging
        Raylib.InitWindow(_width, _height, title);
        _canvasRenderer = new CanvasRenderer();
        _gui.Platform.Register<ICanvasRenderer>(_canvasRenderer);
        _canvasRenderer.Initialize(_width, _height);
    }

    /// <inheritdoc />
    public float ScaleFactor => Raylib.GetWindowScaleDPI().X;

    /// <inheritdoc />
    public Vector2 LogicalSize => new(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    /// <inheritdoc />
    public Vector2 FramebufferSize => LogicalSize * ScaleFactor;

    /// <summary>
    /// Gets a string resource from the assembly's embedded resources.
    /// </summary>
    /// <param name="resource">The name of the resource to retrieve.</param>
    /// <returns>The content of the resource as a string.</returns>
    public static string GetStringResource(string resource)
    {
        using var stream = GetStreamResource(resource);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Gets a stream resource from the assembly's embedded resources.
    /// </summary>
    /// <param name="resource">The name of the resource to retrieve.</param>
    /// <returns>A stream containing the resource data.</returns>
    static Stream GetStreamResource(string resource)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resource);
        if (stream == null) throw new Exception($"Could not load resource: `{resource}`");
        return stream;
    }

    /// <summary>
    /// Runs the GUI application with the specified draw callback.
    /// </summary>
    /// <param name="draw">The callback method that defines the GUI layout and rendering.</param>
    public void RunGui(Action draw)
    {
        while (!Raylib.WindowShouldClose())
        {
            if (_width != Raylib.GetScreenWidth() || _height != Raylib.GetScreenHeight())
            {
                _width = Raylib.GetScreenWidth();
                _height = Raylib.GetScreenHeight();
                _canvasRenderer.Resize(_width, _height);
            }

            _gui.Time.Update(Raylib.GetFrameTime());

            // Update mouse position tracking
            _prevMousePosition = _currentMousePosition;
            _currentMousePosition = Raylib.GetMousePosition();

            // Handle text input
            var keyPressed = Raylib.GetCharPressed();
            while (keyPressed > 0)
            {
                _typedCharacters.Append((char)keyPressed);
                keyPressed = Raylib.GetCharPressed();
            }

            _canvasRenderer.Render(canvas =>
            {
                _gui.SetStage(Pass.Pass1Build);
                _gui.BeginFrame(canvas, _fontText, _fontIcon);
                draw();

                // Process the whole layout after the build pass
                _gui.CalculateLayout();

                _gui.SetStage(Pass.Pass2Render);
                draw();
                _gui.Render();

                _gui.EndFrame();
            });
        }
    }

    /// <summary>
    /// Releases all resources used by the GuiWindow.
    /// </summary>
    public void Dispose()
    {
        _canvasRenderer.Dispose();
        _fontText.Dispose();
        _fontIcon.Dispose();
        Raylib.CloseWindow();
    }

    #region IInputHandler

    /// <summary>
    /// Gets a value indicating whether any key is currently pressed.
    /// </summary>
    public bool IsAnyKeyDown => Raylib.GetKeyPressed() != 0;

    /// <summary>
    /// Gets the mouse movement delta from the previous frame.
    /// </summary>
    public Vector2 MouseDelta => Raylib.GetMouseDelta();

    /// <summary>
    /// Gets the current mouse position.
    /// </summary>
    public Vector2 MousePosition => _currentMousePosition;

    /// <summary>
    /// Gets the mouse wheel scroll delta.
    /// </summary>
    public float MouseWheelDelta => Raylib.GetMouseWheelMove();

    /// <summary>
    /// Gets the previous mouse position.
    /// </summary>
    public Vector2 PrevMousePosition => _prevMousePosition;

    /// <summary>
    /// Determines whether the specified key was just pressed this frame.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key was just pressed; otherwise, false.</returns>
    public bool IsKeyPressed(KeyboardKey keyboardKey) =>
        Raylib.IsKeyPressed((Raylib_cs.KeyboardKey)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently held down.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is held down; otherwise, false.</returns>
    public bool IsKeyDown(KeyboardKey keyboardKey) =>
        Raylib.IsKeyDown((Raylib_cs.KeyboardKey)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently up (not pressed).
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is up; otherwise, false.</returns>
    public bool IsKeyUp(KeyboardKey keyboardKey) =>
        Raylib.IsKeyUp((Raylib_cs.KeyboardKey)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified mouse button was just pressed this frame.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button was just pressed; otherwise, false.</returns>
    public bool IsMouseButtonPressed(MouseButton button) =>
        Raylib.IsMouseButtonPressed((Raylib_cs.MouseButton)(int)button);

    /// <summary>
    /// Determines whether the specified mouse button is currently held down.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is held down; otherwise, false.</returns>
    public bool IsMouseButtonDown(MouseButton button) =>
        Raylib.IsMouseButtonDown((Raylib_cs.MouseButton)(int)button);

    /// <summary>
    /// Determines whether the specified mouse button is currently up (not pressed).
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is up; otherwise, false.</returns>
    public bool IsMouseButtonUp(MouseButton button) =>
        Raylib.IsMouseButtonUp((Raylib_cs.MouseButton)(int)button);

    /// <summary>
    /// Gets all characters typed since the last call to this method.
    /// </summary>
    /// <returns>A string containing all typed characters.</returns>
    public string GetTypedCharacters()
    {
        var result = _typedCharacters.ToString();
        _typedCharacters.Clear();
        return result;
    }

    /// <summary>
    /// Gets the current clipboard text content.
    /// </summary>
    /// <returns>The clipboard text content, or an empty string if retrieval fails.</returns>
    public unsafe string GetClipboardText()
    {
        try
        {
            var ptr = Raylib.GetClipboardText();
            return ptr != null ? new string(ptr) : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Sets the clipboard text content.
    /// </summary>
    /// <param name="text">The text to set in the clipboard.</param>
    public void SetClipboardText(string text)
    {
        try
        {
            Raylib.SetClipboardText(text);
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    #endregion IInputHandler

    #region IWindowHandler

    /// <summary>
    /// Shows or hides the window title bar.
    /// </summary>
    /// <param name="show">True to show the title bar; false to hide it.</param>
    public void DrawWindowTitlebar(bool show)
    {
        // Raylib doesn't allow changing window border after creation
        // This is a no-op but required for interface compliance
    }

    #endregion IWindowHandler
}
