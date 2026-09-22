using System.Numerics;
using System.Reflection;
using System.Text;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Guinevere;

/// <summary>
/// Represents a GUI window implementation using OpenTK for OpenGL rendering.
/// Provides input handling, window management, and rendering capabilities for the Guinevere GUI framework.
/// </summary>
public class GuiWindow : GameWindow, IInputHandler, IWindowHandler, IDisplayCapability, IDisposable
{
    readonly Gui _gui;
    readonly ICanvasRenderer _canvasRenderer;
    Action _guiCallback = null!;
    int _width;
    int _height;
    readonly Font _fontText;
    readonly Font _fontIcon;
    readonly StringBuilder _typedCharacters = new();

    /// <summary>
    /// Initializes a new instance of the GuiWindow class with the specified parameters.
    /// </summary>
    /// <param name="gui">The GUI instance to render.</param>
    /// <param name="width">The initial width of the window. Default is 800.</param>
    /// <param name="height">The initial height of the window. Default is 600.</param>
    /// <param name="title">The title of the window. Default is empty string.</param>
    public GuiWindow(Gui gui, int width = 800, int height = 600, string title = "") : base(GameWindowSettings.Default,
        new NativeWindowSettings { ClientSize = (width, height), Title = title, WindowBorder = WindowBorder.Hidden })
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
        _canvasRenderer = new CanvasRenderer();
        _gui.Platform.Register<ICanvasRenderer>(_canvasRenderer);
        _canvasRenderer.Initialize(_width, _height);

        // Subscribe to text input events
        TextInput += OnTextInput;
    }

    float IDisplayCapability.ScaleFactor => Size.X > 0 ? (float)base.FramebufferSize.X / Size.X : 1f;

    Vector2 IDisplayCapability.LogicalSize => new(Size.X, Size.Y);

    Vector2 IDisplayCapability.FramebufferSize => new(base.FramebufferSize.X, base.FramebufferSize.Y);

    /// <summary>
    /// Handles window resize events by updating internal dimensions and resizing the canvas renderer.
    /// </summary>
    /// <param name="e">The resize event arguments containing the new window dimensions.</param>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        _width = e.Width;
        _height = e.Height;
        _canvasRenderer.Resize(_width, _height);
    }

    /// <summary>
    /// Handles frame update events by updating the GUI time.
    /// </summary>
    /// <param name="args">The frame event arguments.</param>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        _gui.Time.Update(args.Time);
    }

    /// <summary>
    /// Handles text input events by appending typed characters to the input buffer.
    /// </summary>
    /// <param name="e">The text input event arguments.</param>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(e.AsString))
            {
                _typedCharacters.Append(e.AsString);
            }
        }
        catch (Exception ex)
        {
            // Log and ignore text input errors to prevent crashes
            Console.WriteLine($"Text input error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles frame rendering by executing the GUI draw callback and rendering the result.
    /// </summary>
    /// <param name="args">The frame event arguments.</param>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        _canvasRenderer.Render(canvas =>
        {
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(canvas, _fontText, _fontIcon);
            _guiCallback();

            // Process the whole layout after the build pass
            _gui.CalculateLayout();

            _gui.SetStage(Pass.Pass2Render);
            _guiCallback();
            _gui.Render();

            _gui.EndFrame();
        });

        SwapBuffers();
    }

    /// <summary>
    /// Runs the GUI application with the specified draw callback.
    /// </summary>
    /// <param name="draw">The callback method that defines the GUI layout and rendering.</param>
    public void RunGui(Action draw)
    {
        _guiCallback = draw;
        Run();
    }

    /// <summary>
    /// Releases all resources used by the GuiWindow.
    /// </summary>
    public new void Dispose()
    {
        base.Dispose();
        _canvasRenderer.Dispose();
        _fontText.Dispose();
        _fontIcon.Dispose();
    }

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
        var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new Exception($"Could not load resource: `{resource}`");
        return stream;
    }

    #region IInputHandler

    /// <summary>
    /// Gets the mouse movement delta from the previous frame.
    /// </summary>
    public Vector2 MouseDelta => new(MouseState.Delta.X, MouseState.Delta.Y);

    /// <summary>
    /// Gets the current mouse position.
    /// </summary>
    public new Vector2 MousePosition => new(MouseState.Position.X, MouseState.Position.Y);

    /// <summary>
    /// Gets the mouse wheel scroll delta.
    /// </summary>
    public float MouseWheelDelta => MouseState.ScrollDelta.Y;

    /// <summary>
    /// Gets the previous mouse position.
    /// </summary>
    public Vector2 PrevMousePosition => new(MouseState.PreviousPosition.X, MouseState.PreviousPosition.Y);

    /// <summary>
    /// Gets a value indicating whether any key is currently pressed.
    /// </summary>
    public new bool IsAnyKeyDown => KeyboardState.IsAnyKeyDown;

    /// <summary>
    /// Determines whether the specified key was just pressed this frame.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key was just pressed; otherwise, false.</returns>
    public bool IsKeyPressed(KeyboardKey keyboardKey) =>
        IsKeyPressed((Keys)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently held down.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is held down; otherwise, false.</returns>
    public bool IsKeyDown(KeyboardKey keyboardKey) =>
        IsKeyDown((Keys)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently up (not pressed).
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is up; otherwise, false.</returns>
    public bool IsKeyUp(KeyboardKey keyboardKey) =>
        IsKeyReleased((Keys)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified mouse button was just pressed this frame.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button was just pressed; otherwise, false.</returns>
    public bool IsMouseButtonPressed(MouseButton button) =>
        IsMouseButtonPressed((global::OpenTK.Windowing.GraphicsLibraryFramework.MouseButton)(int)button);

    /// <summary>
    /// Determines whether the specified mouse button is currently held down.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is held down; otherwise, false.</returns>
    public bool IsMouseButtonDown(MouseButton button) =>
        IsMouseButtonDown((global::OpenTK.Windowing.GraphicsLibraryFramework.MouseButton)(int)button);

    /// <summary>
    /// Determines whether the specified mouse button is currently up (not pressed).
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is up; otherwise, false.</returns>
    public bool IsMouseButtonUp(MouseButton button) =>
        IsMouseButtonReleased((global::OpenTK.Windowing.GraphicsLibraryFramework.MouseButton)(int)button);

    /// <summary>
    /// Gets all characters typed since the last call to this method.
    /// </summary>
    /// <returns>A string containing all typed characters.</returns>
    public string GetTypedCharacters()
    {
        try
        {
            var result = _typedCharacters.ToString();
            _typedCharacters.Clear();
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetTypedCharacters error: {ex.Message}");
            _typedCharacters.Clear(); // Clear to prevent further issues
            return "";
        }
    }

    /// <summary>
    /// Gets the current clipboard text content.
    /// </summary>
    /// <returns>The clipboard text content, or an empty string if retrieval fails.</returns>
    public unsafe string GetClipboardText()
    {
        try
        {
            var result = GLFW.GetClipboardString(WindowPtr);
            return result ?? "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clipboard get error: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Sets the clipboard text content.
    /// </summary>
    /// <param name="text">The text to set in the clipboard.</param>
    public unsafe void SetClipboardText(string text)
    {
        try
        {
            if (!string.IsNullOrEmpty(text))
            {
                GLFW.SetClipboardString(WindowPtr, text);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clipboard set error: {ex.Message}");
        }
    }

    #endregion IInputHandler

    /// <summary>
    /// Shows or hides the window title bar.
    /// </summary>
    /// <param name="show">True to show the title bar; false to hide it.</param>
    public void DrawWindowTitlebar(bool show) =>
        WindowBorder = show ? WindowBorder.Resizable : WindowBorder.Hidden;
}
