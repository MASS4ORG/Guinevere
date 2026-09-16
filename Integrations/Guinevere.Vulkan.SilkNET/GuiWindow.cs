using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text;
using Silk.NET.Input;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Guinevere.Vulkan.SilkNET;

/// <summary>
/// Represents a GUI window implementation using SilkNET for Vulkan rendering.
/// Provides input handling, window management, and rendering capabilities for the Guinevere GUI framework.
/// </summary>
public unsafe class GuiWindow : IInputHandler, IWindowHandler, IDisposable
{
    private readonly Gui _gui;
    private readonly IWindow _window;
    private readonly CanvasRenderer _renderer;
    private IInputContext _inputContext = null!;
    private IMouse _mouse = null!;
    private IKeyboard _keyboard = null!;
    private Action _draw = null!;
    private bool _isInitialized;
    private Vector2 _mousePosition;
    private Vector2 _prevMousePosition;
    private Vector2 _mouseDelta;
    private float _mouseWheelDelta;
    private readonly HashSet<Silk.NET.Input.MouseButton> _pressedButtons = new();
    private readonly HashSet<Silk.NET.Input.MouseButton> _heldButtons = new();
    private readonly HashSet<Key> _pressedKeys = new();
    private readonly HashSet<Key> _heldKeys = new();
    private readonly StringBuilder _typedCharacters = new();
    private readonly Font _fontText;
    private readonly Font _fontIcon;
    private readonly Glfw _glfw = Glfw.GetApi();

    /// <summary>
    /// Initializes a new instance of the GuiWindow class with the specified parameters.
    /// </summary>
    /// <param name="gui">The GUI instance to render.</param>
    /// <param name="width">The initial width of the window. Default is 800.</param>
    /// <param name="height">The initial height of the window. Default is 600.</param>
    /// <param name="title">The title of the window. Default is empty string.</param>
    public GuiWindow(Gui gui, int width = 800, int height = 600, string title = "")
    {
        _gui = gui;
        _gui.Input = this;
        _gui.WindowHandler = this;
        var fontStream = GetStreamResource("Fonts.font.ttf");
        _fontText = Font.FromStream(fontStream);
        fontStream = GetStreamResource("Fonts.icons.ttf");
        _fontIcon = Font.FromStream(fontStream);
        _renderer = new CanvasRenderer();

        // Create window options with Vulkan API
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = title;
        options.API = new GraphicsAPI(ContextAPI.Vulkan, ContextProfile.Core, ContextFlags.Default,
            new APIVersion(1, 2));
        options.VSync = false;
        // options.UpdatesPerSecond = 60.0;
        // options.FramesPerSecond = 60.0;
        options.ShouldSwapAutomatically = false; // We'll handle swapping manually
        options.WindowState = WindowState.Normal;
        options.IsVisible = true;
        options.WindowBorder = WindowBorder.Resizable;

        _window = Window.Create(options);

        // Hook up all necessary events
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnResize;
        _window.Closing += OnClosing;
        _window.Update += OnUpdate;
    }

    /// <summary>Requests that the native window close.</summary>
    public void Close() => _window.Close();

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
    private static Stream GetStreamResource(string resource)
    {
        resource = "Guinevere.Vulkan.SilkNET." + resource;
        var assembly = Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream(resource);
        if (stream == null) throw new Exception($"Could not load resource: `{resource}`");
        return stream;
    }

    /// <summary>
    /// Handles frame update events by updating the GUI time.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last update.</param>
    private void OnUpdate(double deltaTime)
    {
        _gui.Time.Update(deltaTime);
    }

    /// <summary>
    /// Handles window load events by initializing the Vulkan renderer and input systems.
    /// </summary>
    private void OnLoad()
    {
        Console.WriteLine("OnLoad called");
        // Initialize the Vulkan renderer with our window context
        _renderer.Initialize(_window.Size.X, _window.Size.Y, _window);

        // Initialize input
        _inputContext = _window.CreateInput();
        _mouse = _inputContext.Mice[0];
        _keyboard = _inputContext.Keyboards[0];

        // Hook up mouse events
        _mouse.MouseMove += OnMouseMove;
        _mouse.Scroll += OnMouseScroll;
        _mouse.MouseDown += OnMouseDown;
        _mouse.MouseUp += OnMouseUp;

        // Hook up keyboard events
        _keyboard.KeyDown += OnKeyDown;
        _keyboard.KeyUp += OnKeyUp;
        _keyboard.KeyChar += OnKeyChar;

        _isInitialized = true;
        Console.WriteLine("OnLoad completed");
    }

    /// <summary>
    /// Handles frame rendering by executing the GUI draw callback and rendering the result.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last render.</param>
    private void OnRender(double deltaTime)
    {
        // Make sure we're initialized before rendering
        if (!_isInitialized)
            return;

        // Render our GUI using the Vulkan canvas renderer
        try
        {
            _renderer.Render(canvas =>
            {
                try
                {
                    _gui.SetStage(Pass.Pass1Build);
                    _gui.BeginFrame(canvas, _fontText, _fontIcon);
                    _draw();

                    // Process the whole layout after the build pass
                    _gui.CalculateLayout();

                    _gui.SetStage(Pass.Pass2Render);
                    _draw();
                    _gui.Render();

                    _gui.EndFrame();
                }
                catch (Exception drawEx)
                {
                    // Log draw exceptions for debugging
                    Debug.WriteLine($"Exception in draw callback: {drawEx.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            // Log render exceptions for debugging
            Debug.WriteLine($"Render exception: {ex.Message}");
        }

        // Reset mouse wheel delta and pressed buttons/keys after frame
        _mouseWheelDelta = 0f;
        _pressedButtons.Clear();
        _pressedKeys.Clear();
    }

    /// <summary>
    /// Handles window resize events by updating renderer dimensions.
    /// </summary>
    /// <param name="newSize">The new window size.</param>
    private void OnResize(Vector2D<int> newSize)
    {
        if (!_isInitialized)
            return;

        // Update renderer size
        _renderer.Resize(newSize.X, newSize.Y);
    }

    /// <summary>
    /// Handles window closing events by cleaning up resources.
    /// </summary>
    private void OnClosing()
    {
        _isInitialized = false;
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _prevMousePosition = _mousePosition;
        _mousePosition = position;
        _mouseDelta = _mousePosition - _prevMousePosition;
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        _mouseWheelDelta = scrollWheel.Y;
    }

    private void OnMouseDown(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        _pressedButtons.Add(button);
        _heldButtons.Add(button);
    }

    private void OnMouseUp(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        _heldButtons.Remove(button);
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        _pressedKeys.Add(key);
        _heldKeys.Add(key);
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        _heldKeys.Remove(key);
    }

    private void OnKeyChar(IKeyboard keyboard, char c)
    {
        _typedCharacters.Append(c);
    }

    /// <summary>
    /// Runs the GUI application with the specified draw callback.
    /// </summary>
    /// <param name="draw">The callback method that defines the GUI layout and rendering.</param>
    public void RunGui(Action draw)
    {
        _draw = draw;
        _window.Run();
    }

    /// <summary>
    /// Releases all resources used by the GuiWindow.
    /// </summary>
    public void Dispose()
    {
        _inputContext.Dispose();
        _renderer.Dispose();
        _window.Dispose();
        _fontText.Dispose();
        _fontIcon.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Shows or hides the window title bar.
    /// </summary>
    /// <param name="show">True to show the title bar; false to hide it.</param>
    public void DrawWindowTitlebar(bool show)
    {
        // Silk.NET doesn't easily support changing window border after creation
        // This would require recreating the window, so we'll leave it as no-op for now
    }

    #region IInputHandler

    /// <summary>
    /// Gets a value indicating whether any key is currently pressed.
    /// </summary>
    public bool IsAnyKeyDown => _heldKeys.Count > 0;

    /// <summary>
    /// Gets the mouse movement delta from the previous frame.
    /// </summary>
    public Vector2 MouseDelta => new(_mouseDelta.X, _mouseDelta.Y);

    /// <summary>
    /// Gets the current mouse position.
    /// </summary>
    public Vector2 MousePosition => new(_mouse.Position.X, _mouse.Position.Y);

    /// <summary>
    /// Gets the mouse wheel scroll delta.
    /// </summary>
    public float MouseWheelDelta => _mouseWheelDelta;

    /// <summary>
    /// Gets the previous mouse position.
    /// </summary>
    public Vector2 PrevMousePosition => new(_prevMousePosition.X, _prevMousePosition.Y);

    /// <summary>
    /// Determines whether the specified key was just pressed this frame.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key was just pressed; otherwise, false.</returns>
    public bool IsKeyPressed(KeyboardKey keyboardKey) =>
        _pressedKeys.Contains((Key)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently held down.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is held down; otherwise, false.</returns>
    public bool IsKeyDown(KeyboardKey keyboardKey) =>
        _heldKeys.Contains((Key)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified key is currently up (not pressed).
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is up; otherwise, false.</returns>
    public bool IsKeyUp(KeyboardKey keyboardKey) =>
        !_heldKeys.Contains((Key)(int)keyboardKey) &&
        !_pressedKeys.Contains((Key)(int)keyboardKey);

    /// <summary>
    /// Determines whether the specified mouse button was just pressed this frame.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button was just pressed; otherwise, false.</returns>
    public bool IsMouseButtonPressed(MouseButton button) =>
        _pressedButtons.Contains((Silk.NET.Input.MouseButton)(int)button);

    /// <summary>
    /// Determines whether the specified mouse button is currently held down.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is held down; otherwise, false.</returns>
    public bool IsMouseButtonDown(MouseButton button) =>
        _heldButtons.Contains((Silk.NET.Input.MouseButton)(int)button);

    /// <summary>
    /// Determines whether the specified mouse button is currently up (not pressed).
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is up; otherwise, false.</returns>
    public bool IsMouseButtonUp(MouseButton button) =>
        !_heldButtons.Contains((Silk.NET.Input.MouseButton)(int)button) &&
        !_pressedButtons.Contains((Silk.NET.Input.MouseButton)(int)button);

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
    public string GetClipboardText()
    {
        try
        {
            return _glfw.GetClipboardString((WindowHandle*)_window.Handle) ?? "";
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
            _glfw.SetClipboardString((WindowHandle*)_window.Handle, text);
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    #endregion IInputHandler
}
