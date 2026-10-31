using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using static Guinevere.AtSpiNative;

namespace Guinevere;

sealed partial class AtSpiConnection : IDisposable
{
    const string Root = "/org/a11y/atspi/accessible/root";
    const string Window = "/org/a11y/atspi/accessible/window";
    const string Null = "/org/a11y/atspi/null";
    const string Cache = "/org/a11y/atspi/cache";
    const string Prefix = "org.a11y.atspi.";
    readonly Action<string, AccessibilityAction> _enqueue;
    readonly Thread _thread;
    readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly ConcurrentQueue<(string Scope, string Message)> _announcements = new();
    readonly Dictionary<string, List<uint>> _registrations = new(StringComparer.Ordinal);
    readonly Method _method;
    readonly GetProperty _property;
    readonly SetProperty _setProperty;
    readonly Ready _embedded;
    readonly string _title;
    readonly Func<string> _getAddress;
    readonly object _lifetime = new();
    Dictionary<string, AccessibilityElement> _elements = [];
    Dictionary<string, List<string>> _children = [];
    Frame? _pending;
    Vector2 _origin;
    nint _context;
    nint _connection;
    nint _info;
    nint _vtable;
    string _bus = "";
    string _parent = "('', objectpath '/org/a11y/atspi/null')";
    volatile bool _stopping;
    int _disposed;
    int _applicationId;
    internal string BusName => _bus;

    internal AtSpiConnection(string title, Action<string, AccessibilityAction> enqueue, Func<string>? getAddress = null)
    {
        _title = title;
        _getAddress = getAddress ?? GetAddress;
        _enqueue = enqueue;
        _method = OnMethod;
        _property = OnProperty;
        _setProperty = (_, _, _, _, property, value, _, _) =>
        {
            if (Utf8(property) != "Id")
            {
                return 0;
            }
            _applicationId = g_variant_get_int32(value);
            return 1;
        };
        _embedded = OnEmbedded;
        _thread = new Thread(Run) { IsBackground = true, Name = "Guinevere AT-SPI" };
        _thread.Start();
        try
        {
            _started.Task.WaitAsync(TimeSpan.FromSeconds(6)).GetAwaiter().GetResult();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void Publish(AccessibilityElement[] elements, (string Scope, string Message)[] announcements,
        Vector2 origin, Rect windowBounds)
    {
        Interlocked.Exchange(ref _pending, new Frame(elements, origin, windowBounds));
        foreach (var announcement in announcements)
        {
            _announcements.Enqueue(announcement);
        }
        Wake();
    }

    void Run()
    {
        try
        {
            _context = g_main_context_new();
            g_main_context_push_thread_default(_context);
            Connect();
            while (!_stopping)
            {
                ApplyFrame();
                g_main_context_iteration(_context, 1);
            }
        }
        catch (Exception e)
        {
            _started.TrySetException(e);
            System.Diagnostics.Trace.WriteLine($"AT-SPI connection stopped: {e.Message}");
        }
        finally
        {
            Close();
        }
    }

    void Connect()
    {
        _connection = g_dbus_connection_new_for_address_sync(_getAddress(), 9, 0, 0, out var error);
        Check(error);
        _bus = Utf8(g_dbus_connection_get_unique_name(_connection));
        RegisterApplication();
    }

    static string GetAddress()
    {
        var session = g_bus_get_sync(2, 0, out var error);
        Check(error);
        string address;
        try
        {
            var reply = g_dbus_connection_call_sync(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus",
                "GetAddress", 0, 0, 0, 2000, 0, out error);
            Check(error);
            address = ChildString(reply, 0);
            g_variant_unref(reply);
        }
        finally
        {
            g_object_unref(session);
        }
        return address;
    }

    void RegisterApplication()
    {
        _info = g_dbus_node_info_new_for_xml(Introspection, out var error);
        Check(error);
        _vtable = Marshal.AllocHGlobal(IntPtr.Size * 11);
        for (var i = 0; i < 11; i++)
        {
            Marshal.WriteIntPtr(_vtable, i * IntPtr.Size, 0);
        }
        Marshal.WriteIntPtr(_vtable, Marshal.GetFunctionPointerForDelegate(_method));
        Marshal.WriteIntPtr(_vtable, IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_property));
        Marshal.WriteIntPtr(_vtable, IntPtr.Size * 2, Marshal.GetFunctionPointerForDelegate(_setProperty));
        _elements[Root] = new("root", null, "Application", _title, "", default, ControlVisualState.None, Actions: []);
        _elements[Window] = new("window", "root", "Window", _title, "", default, ControlVisualState.None, Actions: []);
        BuildChildren();
        Register(Root, true);
        Register(Window, false);
        var cacheInfo = g_dbus_node_info_lookup_interface(_info, Prefix + "Cache");
        var cacheId = g_dbus_connection_register_object(_connection, Cache, cacheInfo, _vtable, 0, 0, out error);
        Check(error);
        _registrations.Add(Cache, [cacheId]);
        var plug = Variant($"({Reference(Root)},)");
        try
        {
            g_dbus_connection_call(_connection, "org.a11y.atspi.Registry", Root, Prefix + "Socket", "Embed",
                plug, 0, 0, 3000, 0, _embedded, 0);
        }
        finally
        {
            g_variant_unref(plug);
        }
    }

    void OnEmbedded(nint source, nint result, nint data)
    {
        try
        {
            var reply = g_dbus_connection_call_finish(source, result, out var error);
            Check(error);
            var parent = g_variant_get_child_value(reply, 0);
            _parent = $"({Quote(ChildString(parent, 0))}, objectpath {Quote(ChildString(parent, 1))})";
            g_variant_unref(parent);
            g_variant_unref(reply);
            _started.TrySetResult();
        }
        catch (Exception e)
        {
            _started.TrySetException(e);
        }
    }

    void Register(string path, bool application)
    {
        var registrations = new List<uint>();
        _registrations.Add(path, registrations);
        foreach (var name in application ? new[] { "Accessible", "Application", "Component", "Action" }
                     : ["Accessible", "Component", "Action"])
        {
            var info = g_dbus_node_info_lookup_interface(_info, Prefix + name);
            var id = g_dbus_connection_register_object(_connection, path, info, _vtable, 0, 0, out var error);
            Check(error);
            registrations.Add(id);
        }
    }

    void ApplyFrame()
    {
        if (Interlocked.Exchange(ref _pending, null) is { } frame)
        {
            _origin = frame.Origin;
            var next = frame.Elements.ToDictionary(static e => Path(e.Id), StringComparer.Ordinal);
            next[Root] = _elements[Root];
            next[Window] = _elements[Window] with { Bounds = frame.WindowBounds };
            var previous = _elements;
            _elements = next;
            BuildChildren();
            foreach (var (path, element) in next)
            {
                ApplyElement(path, element, previous);
            }
            foreach (var (path, element) in previous)
            {
                if (next.ContainsKey(path))
                {
                    continue;
                }
                RemoveElement(path, element, previous);
            }
        }
        while (_announcements.TryDequeue(out var announcement))
        {
            var path = Path(announcement.Scope);
            Emit(_elements.ContainsKey(path) ? path : Window, "Announcement", "", 2, Quote(announcement.Message));
        }
    }

    void ApplyElement(string path, AccessibilityElement element, Dictionary<string, AccessibilityElement> previous)
    {
        if (!_registrations.ContainsKey(path))
        {
            Register(path, false);
            EmitCache("AddAccessible", CacheItem(path, element));
            Emit(ParentPath(element), "ChildrenChanged", "add", Children(ParentPath(element)).IndexOf(path), Reference(path));
        }
        previous.TryGetValue(path, out var old);
        if (old is not null && old.ParentId != element.ParentId)
        {
            Emit(ParentPath(old, previous), "ChildrenChanged", "remove", -1, Reference(path));
            Emit(ParentPath(element), "ChildrenChanged", "add", Children(ParentPath(element)).IndexOf(path), Reference(path));
            Emit(path, "PropertyChange", "accessible-parent", 0, Reference(ParentPath(element)));
        }
        PublishChanges(path, old, element);
    }

    void RemoveElement(string path, AccessibilityElement element, Dictionary<string, AccessibilityElement> previous)
    {
        Emit(ParentPath(element, previous), "ChildrenChanged", "remove", -1, Reference(path));
        EmitCache("RemoveAccessible", Reference(path));
        foreach (var registration in _registrations[path])
        {
            g_dbus_connection_unregister_object(_connection, registration);
        }
        _registrations.Remove(path);
    }

    void PublishChanges(string path, AccessibilityElement? old, AccessibilityElement element)
    {
        PublishFlags(path, old, element);
        if (old is not null && old.Name != element.Name)
        {
            Emit(path, "PropertyChange", "accessible-name", 0, Quote(element.Name));
        }
        if (old is not null && old.Description != element.Description)
        {
            Emit(path, "PropertyChange", "accessible-description", 0, Quote(element.Description));
        }
        PublishExpansion(path, old, element);
        if (old is not null && old.Bounds != element.Bounds)
        {
            var bounds = element.Bounds;
            Emit(path, "BoundsChanged", "", 0, $"({(int)bounds.X}, {(int)bounds.Y}, {(int)bounds.W}, {(int)bounds.H})");
        }
    }

    void PublishExpansion(string path, AccessibilityElement? old, AccessibilityElement element)
    {
        if (old?.Expanded != element.Expanded && element.Expanded is { } expanded)
        {
            Emit(path, "StateChanged", "expanded", expanded ? 1 : 0, "0");
        }
    }

    void PublishFlags(string path, AccessibilityElement? old, AccessibilityElement element)
    {
        foreach (var (flag, name) in new[] { (ControlVisualState.Focused, "focused"),
                     (ControlVisualState.Selected, "selected"), (ControlVisualState.Checked, "checked") })
        {
            var value = element.State.HasFlag(flag);
            PublishState(path, name, old?.State.HasFlag(flag) ?? false, value);
        }
        PublishState(path, "showing", old is { Virtualized: false }, !element.Virtualized);
        PublishEnabled(path, old, element);
    }

    void PublishEnabled(string path, AccessibilityElement? old, AccessibilityElement element)
    {
        var wasEnabled = old is not null && !old.State.HasFlag(ControlVisualState.Disabled);
        var enabled = !element.State.HasFlag(ControlVisualState.Disabled);
        PublishState(path, "enabled", wasEnabled, enabled);
        PublishState(path, "sensitive", wasEnabled, enabled);
    }

    void PublishState(string path, string name, bool previous, bool current)
    {
        if (previous != current)
        {
            Emit(path, "StateChanged", name, current ? 1 : 0, "0");
        }
    }

    void Emit(string path, string signal, string detail, int value, string payload)
    {
        var parameters = Variant($"({Quote(detail)}, {value}, 0, <{payload}>, @a{{sv}} {{}})");
        try
        {
            g_dbus_connection_emit_signal(_connection, 0, path, Prefix + "Event.Object", signal, parameters, out var error);
            Check(error);
        }
        finally
        {
            g_variant_unref(parameters);
        }
    }

    void EmitCache(string signal, string item)
    {
        var value = Variant($"({item},)");
        try
        {
            g_dbus_connection_emit_signal(_connection, 0, Cache, Prefix + "Cache", signal, value, out var error);
            Check(error);
        }
        finally
        {
            g_variant_unref(value);
        }
    }

    string CacheItem(string path, AccessibilityElement element) =>
        $"({Reference(path)}, {Reference(Root)}, {(path == Root ? _parent : Reference(ParentPath(element)))}, "
        + $"{(path == Root ? -1 : Children(ParentPath(element)).IndexOf(path))}, {Children(path).Count}, "
        + $"{InterfaceArray(path)}, {Quote(element.Name)}, uint32 {Role(element.Role)}, {Quote(element.Description)}, {State(element)})";

    static string Path(string id) => "/org/a11y/atspi/accessible/e" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
    static string Quote(string text) => "'" + text.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("'", "\\'", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal) + "'";
    string Reference(string path) => $"({Quote(path == Null ? "" : _bus)}, objectpath {Quote(path)})";
    string ParentPath(AccessibilityElement element) => ParentPath(element, _elements);
    static string ParentPath(AccessibilityElement element, Dictionary<string, AccessibilityElement> elements) =>
        element.Id == "window" ? Root
            : element.ParentId is { } parent && elements.ContainsKey(Path(parent)) ? Path(parent) : Window;
    List<string> Children(string path) => _children.TryGetValue(path, out var children) ? children : [];

    void BuildChildren()
    {
        _children = new(StringComparer.Ordinal) { [Root] = [Window] };
        foreach (var (path, element) in _elements)
        {
            if (path is Root or Window)
            {
                continue;
            }
            var parent = ParentPath(element);
            if (!_children.TryGetValue(parent, out var children))
            {
                _children[parent] = children = [];
            }
            children.Add(path);
        }
    }

    void Wake()
    {
        lock (_lifetime)
        {
            if (_context != 0)
            {
                g_main_context_wakeup(_context);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _stopping = true;
        Wake();
        _thread.Join();
    }

    void Close()
    {
        if (_connection != 0)
        {
            foreach (var ids in _registrations.Values)
            {
                foreach (var id in ids)
                {
                    g_dbus_connection_unregister_object(_connection, id);
                }
            }
            g_dbus_connection_close_sync(_connection, 0, out var error);
            if (error != 0)
            {
                g_error_free(error);
            }
            g_object_unref(_connection);
        }
        if (_info != 0)
        {
            g_dbus_node_info_unref(_info);
        }
        if (_vtable != 0)
        {
            Marshal.FreeHGlobal(_vtable);
        }
        lock (_lifetime)
        {
            if (_context != 0)
            {
                g_main_context_pop_thread_default(_context);
                g_main_context_unref(_context);
                _context = 0;
            }
        }
        GC.KeepAlive(_method);
        GC.KeepAlive(_property);
        GC.KeepAlive(_setProperty);
        GC.KeepAlive(_embedded);
    }

    sealed record Frame(AccessibilityElement[] Elements, Vector2 Origin, Rect WindowBounds);
}
