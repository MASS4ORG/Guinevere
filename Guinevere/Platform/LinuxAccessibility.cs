using System.Collections.Concurrent;

namespace Guinevere;

/// <summary>Exports completed semantic frames to the Linux AT-SPI accessibility bus.</summary>
public sealed class LinuxAccessibility : IAccessibilityCapability, IAccessibilityTreeSink,
    IAccessibilityActionSource, IControlActivationSource, IDisposable
{
    readonly Gui _gui;
    readonly Func<Vector2> _origin;
    readonly Action? _focusWindow;
    readonly Dictionary<string, AccessibilityElement> _frame = new(StringComparer.Ordinal);
    readonly List<(string Scope, string Message)> _announcements = [];
    readonly ConcurrentQueue<(string Scope, AccessibilityAction Action)> _requests = new();
    readonly HashSet<string> _activations = [];
    readonly AtSpiConnection? _connection;
    internal string? BusName => _connection?.BusName;

    internal LinuxAccessibility(Gui gui, string title, Func<Vector2> origin, bool connect, Action? focusWindow = null,
        Func<string>? getAddress = null)
    {
        _gui = gui;
        _origin = origin;
        _focusWindow = focusWindow;
        if (connect)
        {
            _connection = new AtSpiConnection(title, Enqueue, getAddress);
        }
    }

    /// <summary>Connects and registers only when the native accessibility bus is available.</summary>
    public static LinuxAccessibility? TryRegister(Gui gui, string title, Func<Vector2> origin, Action? focusWindow = null)
        => TryRegister(gui, () => new LinuxAccessibility(gui, title, origin, true, focusWindow));

    internal static LinuxAccessibility? TryRegister(Gui gui, Func<LinuxAccessibility> create)
    {
        if (!OperatingSystem.IsLinux() || gui.Platform.Supports<IAccessibilityCapability>())
        {
            return null;
        }
        LinuxAccessibility? bridge = null;
        try
        {
            bridge = create();
            gui.Platform.Register<IAccessibilityCapability>(bridge);
            gui.Platform.Register<IAccessibilityTreeSink>(bridge);
            gui.Platform.Register<IAccessibilityActionSource>(bridge);
            gui.Platform.Register<IControlActivationSource>(bridge);
            return bridge;
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException
                                     or InvalidOperationException or TimeoutException)
        {
            bridge?.Dispose();
            System.Diagnostics.Trace.WriteLine($"AT-SPI unavailable: {e.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public void BeginFrame()
    {
        _frame.Clear();
        _announcements.Clear();
        _activations.Clear();
        var count = _requests.Count;
        if (count > 0)
        {
            _focusWindow?.Invoke();
        }
        for (var i = 0; i < count && _requests.TryDequeue(out var request); i++)
        {
            if (request.Scope.Length > 0)
            {
                _requests.Enqueue(request);
                continue;
            }
            if (request.Action.Key is not { } id)
            {
                continue;
            }
            _gui.Focus.RequestFocus(id, FocusReason.Keyboard);
            if (request.Action.Name == "Activate")
            {
                _activations.Add(id);
            }
            _gui.RequestFrame();
        }
    }

    /// <inheritdoc />
    public void Publish(ControlSemantics semantics)
    {
        PublishElement(new AccessibilityElement(semantics.Id, null, semantics.Role.ToString(),
            semantics.Label ?? "", semantics.Value ?? "", semantics.Bounds, semantics.State,
            Actions: semantics.Role is ControlRole.None or ControlRole.TextInput ? ["Focus"] : ["Focus", "Activate"]));
    }

    /// <inheritdoc />
    public void PublishElement(AccessibilityElement element) => _frame[element.Id] = element;

    /// <inheritdoc />
    public void Announce(string scopeId, string message, string? elementId = null)
    {
        if (elementId is not null && _frame.TryGetValue(elementId, out var element)
            && element.Description.Length > 0)
        {
            message += ". " + element.Description;
        }
        _announcements.Add((scopeId, message));
    }

    /// <inheritdoc />
    public void EndFrame() => _connection?.Publish([.. _frame.Values], [.. _announcements], _origin(), _gui.ScreenRect);

    /// <inheritdoc />
    public bool IsActivated(string controlId) => _activations.Contains(controlId);

    /// <inheritdoc />
    public bool TryDequeue(string scopeId, out AccessibilityAction action)
    {
        var count = _requests.Count;
        for (var i = 0; i < count && _requests.TryDequeue(out var request); i++)
        {
            if (request.Scope == scopeId)
            {
                action = request.Action;
                return true;
            }
            _requests.Enqueue(request);
        }
        action = null!;
        return false;
    }

    internal void Enqueue(string scope, AccessibilityAction action)
    {
        _requests.Enqueue((scope, action));
        _gui.RequestFrame();
    }

    /// <summary>Unregisters the owned capabilities and closes the native bus after the last GUI frame.</summary>
    public void Dispose()
    {
        _connection?.Dispose();
        RemoveIfOwned<IAccessibilityCapability>();
        RemoveIfOwned<IAccessibilityTreeSink>();
        RemoveIfOwned<IAccessibilityActionSource>();
        RemoveIfOwned<IControlActivationSource>();
    }

    void RemoveIfOwned<T>() where T : class, IPlatformCapability
    {
        if (_gui.Platform.TryGet<T>(out var value) && ReferenceEquals(value, this))
        {
            _gui.Platform.Remove<T>();
        }
    }
}
