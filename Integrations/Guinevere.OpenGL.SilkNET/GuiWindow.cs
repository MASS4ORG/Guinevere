using System.Numerics;
using System.Reflection;
using System.Text;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Guinevere;

/// <summary>
/// Represents a GUI window implementation using SilkNET for OpenGL rendering.
/// Provides input handling, window management, and rendering capabilities for the Guinevere GUI framework.
/// </summary>
public unsafe class GuiWindow : IInputHandler, IWindowHandler, IDisplayCapability, ICursorCapability,
    IPointerCapability, IDisposable
{
    readonly Gui _gui;
    readonly IWindow _window;
    readonly CanvasRenderer _renderer;
    IInputContext _inputContext = null!;
    IMouse _mouse = null!;
    IKeyboard _keyboard = null!;
    Action _draw = null!;
    GL? _gl;
    bool _isInitialized;
    Vector2 _mousePosition;
    Vector2 _prevMousePosition;
    Vector2 _mouseDelta;
    float _mouseWheelDelta;
    readonly HashSet<Silk.NET.Input.MouseButton> _pressedButtons = [];
    readonly HashSet<Silk.NET.Input.MouseButton> _heldButtons = [];
    readonly HashSet<Key> _pressedKeys = [];
    readonly HashSet<Key> _heldKeys = [];
    readonly StringBuilder _typedCharacters = new();
    readonly Font _fontText;
    readonly Font _fontIcon;
    readonly Font _fontWidgetIcon;
    readonly Glfw _glfw = Glfw.GetApi();

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
        _gui.Platform.Register<IDisplayCapability>(this);
        _gui.Platform.Register<ICursorCapability>(this);
        _gui.Platform.Register<IPointerCapability>(this);
        var fontStream = GetStreamResource("Guinevere.font.ttf");
        _fontText = Font.FromStream(fontStream);
        fontStream = GetStreamResource("Guinevere.icons.ttf");
        _fontIcon = Font.FromStream(fontStream);
        fontStream = GetStreamResource("Guinevere.widget-icons.ttf");
        _fontWidgetIcon = Font.FromStream(fontStream);
        _gui.ConfigureFonts(_fontText, _fontIcon, _fontWidgetIcon);
        _renderer = new CanvasRenderer();
        _gui.Platform.Register<ICanvasRenderer>(_renderer);

        // Create windowHandler options with more explicit settings
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(width, height);
        options.Title = title;
        options.VSync = false;
        options.WindowBorder = WindowBorder.Hidden;
        options.WindowBorder = WindowBorder.Resizable;
        options.API = GraphicsAPI.Default;
        // options.FramesPerSecond = 60;
        // options.UpdatesPerSecond = 60;

        _window = Window.Create(options);

        // Hook up all necessary events
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnResize;
        _window.Closing += OnClosing;
        _window.Update += OnUpdate;
    }

    /// <inheritdoc />
    public float ScaleFactor => _window.Size.X > 0 ? (float)_window.FramebufferSize.X / _window.Size.X : 1f;

    /// <inheritdoc />
    public Vector2 LogicalSize => new(_window.Size.X, _window.Size.Y);

    /// <inheritdoc />
    public Vector2 FramebufferSize => new(_window.FramebufferSize.X, _window.FramebufferSize.Y);

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

    /// <summary>
    /// Handles frame update events by updating the GUI time.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last update.</param>
    void OnUpdate(double deltaTime)
    {
        _gui.Time.Update(deltaTime);
    }

    /// <summary>
    /// Handles window load events by initializing the OpenGL renderer and input systems.
    /// </summary>
    void OnLoad()
    {
        // Create OpenGL context
        _gl = _window.CreateOpenGL();

        // Initialize the renderer with our GL context
        _renderer.Initialize(_window.Size.X, _window.Size.Y, _gl);

        // Initialize input
        _inputContext = _window.CreateInput();
        _mouse = _inputContext.Mice[0];
        Cursor = _cursor;
        ApplyCursorMode();
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
    }

    /// <summary>
    /// Handles frame rendering by executing the GUI draw callback and rendering the result.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last render.</param>
    void OnRender(double deltaTime)
    {
        // Make sure we're initialized before rendering
        if (!_isInitialized || _gl == null)
            return;

        // Pass2Render our GUI using the canvas renderer
        _renderer.Render(canvas =>
        {
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(canvas);
            _draw.Invoke();

            // Process the whole layout after the build pass
            _gui.CalculateLayout();

            _gui.SetStage(Pass.Pass2Render);
            _draw.Invoke();
            _gui.Render();

            _gui.EndFrame();
        });

        // Reset mouse wheel delta and pressed buttons/keys after frame
        _mouseWheelDelta = 0f;
        _pressedButtons.Clear();
        _pressedKeys.Clear();
    }

    /// <summary>
    /// Handles window resize events by updating OpenGL viewport and renderer dimensions.
    /// </summary>
    /// <param name="newSize">The new window size.</param>
    void OnResize(Vector2D<int> newSize)
    {
        if (!_isInitialized || _gl == null)
            return;

        // Update viewport
        _gl.Viewport(0, 0, (uint)newSize.X, (uint)newSize.Y);

        // Update renderer size
        _renderer.Resize(newSize.X, newSize.Y);
    }

    /// <summary>
    /// Handles window closing events by cleaning up resources.
    /// </summary>
    void OnClosing()
    {
        _isInitialized = false;
    }

    void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _prevMousePosition = _mousePosition;
        _mousePosition = position;
        _mouseDelta = _mousePosition - _prevMousePosition;
    }

    void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        _mouseWheelDelta = scrollWheel.Y;
    }

    void OnMouseDown(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        _pressedButtons.Add(button);
        _heldButtons.Add(button);
    }

    void OnMouseUp(IMouse mouse, Silk.NET.Input.MouseButton button)
    {
        _heldButtons.Remove(button);
    }

    void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        _pressedKeys.Add(key);
        _heldKeys.Add(key);
    }

    void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        _heldKeys.Remove(key);
    }

    void OnKeyChar(IKeyboard keyboard, char c)
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
    /// Ends the run loop, so <see cref="RunGui"/> returns and the caller can shut down in order.
    /// Safe to call from inside the draw callback: the window closes at the end of the frame.
    /// </summary>
    public void Close() => _window.Close();

    /// <summary>
    /// Releases all resources used by the GuiWindow.
    /// </summary>
    public void Dispose()
    {
        _inputContext.Dispose();
        _window.Dispose();
        _fontText.Dispose();
        _fontIcon.Dispose();
        _fontWidgetIcon.Dispose();
        // _renderer.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Shows or hides the window title bar.
    /// </summary>
    /// <param name="show">True to show the title bar; false to hide it.</param>
    public void DrawWindowTitlebar(bool show)
    {
        // Silk.NET doesn't easily support changing windowHandler border after creation
        // This would require recreating the windowHandler, so we'll leave it as no-op for now
    }

    #region Cursor and pointer

    PointerCursor _cursor;
    bool _pointerVisible = true;
    bool _pointerLocked;

    /// <inheritdoc />
    public PointerCursor Cursor
    {
        get => _cursor;
        set
        {
            _cursor = value;
            if (_mouse is not null) _mouse.Cursor.StandardCursor = ToStandardCursor(value);
        }
    }

    /// <inheritdoc />
    public bool Visible
    {
        get => _pointerVisible;
        set
        {
            _pointerVisible = value;
            ApplyCursorMode();
        }
    }

    /// <inheritdoc />
    public bool Locked
    {
        get => _pointerLocked;
        set
        {
            _pointerLocked = value;
            ApplyCursorMode();
        }
    }

    /// <inheritdoc />
    public void Warp(Vector2 position)
    {
        if (_mouse is null) return;
        _mouse.Position = position;
        _mousePosition = _prevMousePosition = position;
        _mouseDelta = Vector2.Zero;
    }

    /// <summary>GLFW's disabled mode hides the pointer and reports unbounded virtual positions.</summary>
    void ApplyCursorMode()
    {
        if (_mouse is null) return;
        _mouse.Cursor.CursorMode = _pointerLocked ? CursorMode.Disabled
            : _pointerVisible ? CursorMode.Normal
            : CursorMode.Hidden;
    }

    static StandardCursor ToStandardCursor(PointerCursor cursor) => cursor switch
    {
        PointerCursor.Arrow => StandardCursor.Arrow,
        PointerCursor.Text => StandardCursor.IBeam,
        PointerCursor.Hand => StandardCursor.Hand,
        PointerCursor.Crosshair => StandardCursor.Crosshair,
        PointerCursor.ResizeHorizontal => StandardCursor.HResize,
        PointerCursor.ResizeVertical => StandardCursor.VResize,
        PointerCursor.ResizeDiagonalNorthWestSouthEast => StandardCursor.NwseResize,
        PointerCursor.ResizeDiagonalNorthEastSouthWest => StandardCursor.NeswResize,
        PointerCursor.NotAllowed => StandardCursor.NotAllowed,
        _ => StandardCursor.Default
    };

    #endregion Cursor and pointer

    #region IInputHandler

    /// <summary>
    /// Gets a value indicating whether any key is currently pressed.
    /// </summary>
    public bool IsAnyKeyDown => _heldKeys.Count > 0;

    /// <summary>
    /// Gets the mouse movement delta from the previous frame.
    /// </summary>
    public Vector2 MouseDelta => _mouseDelta;

    /// <summary>
    /// Gets the current mouse position.
    /// </summary>
    public Vector2 MousePosition => _mousePosition;

    /// <summary>
    /// Gets the mouse wheel scroll delta.
    /// </summary>
    public float MouseWheelDelta => _mouseWheelDelta;

    /// <summary>
    /// Gets the previous mouse position.
    /// </summary>
    public Vector2 PrevMousePosition => _prevMousePosition;

    /// <summary>
    /// Determines whether the specified key was just pressed this frame.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key was just pressed; otherwise, false.</returns>
    public bool IsKeyPressed(KeyboardKey keyboardKey)
    {
        return _pressedKeys.Contains((Key)(int)keyboardKey);
    }

    /// <summary>
    /// Determines whether the specified key is currently held down.
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is held down; otherwise, false.</returns>
    public bool IsKeyDown(KeyboardKey keyboardKey)
    {
        return _heldKeys.Contains((Key)(int)keyboardKey);
    }

    /// <summary>
    /// Determines whether the specified key is currently up (not pressed).
    /// </summary>
    /// <param name="keyboardKey">The key to check.</param>
    /// <returns>True if the key is up; otherwise, false.</returns>
    public bool IsKeyUp(KeyboardKey keyboardKey)
    {
        return !_heldKeys.Contains((Key)(int)keyboardKey) &&
               !_pressedKeys.Contains((Key)(int)keyboardKey);
    }

    /// <summary>
    /// Determines whether the specified mouse button was just pressed this frame.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button was just pressed; otherwise, false.</returns>
    public bool IsMouseButtonPressed(MouseButton button)
    {
        return _pressedButtons.Contains((Silk.NET.Input.MouseButton)(int)button);
    }

    /// <summary>
    /// Determines whether the specified mouse button is currently held down.
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is held down; otherwise, false.</returns>
    public bool IsMouseButtonDown(MouseButton button)
    {
        return _heldButtons.Contains((Silk.NET.Input.MouseButton)(int)button);
    }

    /// <summary>
    /// Determines whether the specified mouse button is currently up (not pressed).
    /// </summary>
    /// <param name="button">The mouse button to check.</param>
    /// <returns>True if the button is up; otherwise, false.</returns>
    public bool IsMouseButtonUp(MouseButton button)
    {
        return !_heldButtons.Contains((Silk.NET.Input.MouseButton)(int)button) &&
               !_pressedButtons.Contains((Silk.NET.Input.MouseButton)(int)button);
    }

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
