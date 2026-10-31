using static Guinevere.AtSpiNative;

namespace Guinevere;

sealed partial class AtSpiConnection
{
    delegate string Handler(AtSpiConnection connection, string path, AccessibilityElement element, nint parameters);
    delegate string InterfaceHandler(AtSpiConnection connection, string path, AccessibilityElement element,
        string method, nint parameters);
    static readonly string[] Interfaces = [Prefix + "Accessible", Prefix + "Component", Prefix + "Action"];
    static readonly Dictionary<string, InterfaceHandler> Methods = new(StringComparer.Ordinal)
    {
        [Prefix + "Accessible"] = static (c, p, e, m, a) => c.AccessibleMethod(p, e, m, a),
        [Prefix + "Action"] = static (c, _, e, m, a) => c.ActionMethod(e, m, a),
        [Prefix + "Component"] = static (c, p, e, m, a) => c.ComponentMethod(p, e, m, a),
        [Prefix + "Application"] = static (_, _, _, m, _) => m == "GetLocale"
            ? $"({Quote(System.Globalization.CultureInfo.CurrentCulture.Name)},)" : "('',)"
    };

    void OnMethod(nint connection, nint sender, string path, string iface, string method,
        nint parameters, nint invocation, nint data)
    {
        try
        {
            if (iface == Prefix + "Cache" && method == "GetItems")
            {
                Reply(invocation, $"(@a((so)(so)(so)iiassusau) [{string.Join(",", _elements.Select(p => CacheItem(p.Key, p.Value)))}],)");
                return;
            }
            if (!_elements.TryGetValue(path, out var element) || !Methods.TryGetValue(iface, out var handler))
            {
                throw new InvalidOperationException("The accessible or interface is no longer present.");
            }
            Reply(invocation, handler(this, path, element, method, parameters));
        }
        catch (Exception e)
        {
            g_dbus_method_invocation_return_dbus_error(invocation, "org.freedesktop.DBus.Error.Failed", e.Message);
        }
    }

    static void Reply(nint invocation, string reply)
    {
        var value = Variant(reply);
        try
        {
            g_dbus_method_invocation_return_value(invocation, value);
        }
        finally
        {
            g_variant_unref(value);
        }
    }

    static readonly Dictionary<string, Handler> Properties = new(StringComparer.Ordinal)
    {
        ["version"] = static (_, _, _, _) => "uint32 1",
        ["InterfaceVersion"] = static (_, _, _, _) => "uint32 1",
        ["Name"] = static (_, _, e, _) => Quote(e.Name),
        ["Description"] = static (_, _, e, _) => Quote(e.Description),
        ["Parent"] = static (c, p, e, _) => p == Root ? c._parent : c.Reference(c.ParentPath(e)),
        ["ChildCount"] = static (c, p, _, _) => Integer(c.Children(p).Count),
        ["Locale"] = static (_, _, _, _) => Quote(System.Globalization.CultureInfo.CurrentCulture.Name),
        ["AccessibleId"] = static (_, _, e, _) => Quote(e.Id),
        ["HelpText"] = static (_, _, _, _) => "''",
        ["NActions"] = static (_, _, e, _) => Integer(Actions(e).Count),
        ["ToolkitName"] = static (_, _, _, _) => "'Guinevere'",
        ["Version"] = static (_, _, _, _) => ToolkitVersion(),
        ["ToolkitVersion"] = static (_, _, _, _) => ToolkitVersion(),
        ["AtspiVersion"] = static (_, _, _, _) => "'2.1'",
        ["Id"] = static (c, _, _, _) => Integer(c._applicationId)
    };

    internal nint OnProperty(nint connection, nint sender, string path, string iface, string property, nint error, nint data)
    {
        try
        {
            if (path == Cache && property == "version")
            {
                return Variant("uint32 1");
            }
            return _elements.TryGetValue(path, out var element) && Properties.TryGetValue(property, out var handler)
                ? Variant(handler(this, path, element, 0)) : 0;
        }
        catch (Exception e)
        {
            System.Diagnostics.Trace.WriteLine($"AT-SPI property failed: {e.Message}");
            return 0;
        }
    }

    static string Integer(int number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    static string ToolkitVersion() => Quote(typeof(Gui).Assembly.GetName().Version?.ToString() ?? "");

    static readonly Dictionary<string, Handler> AccessibleMethods = new(StringComparer.Ordinal)
    {
        ["GetChildAtIndex"] = static (c, p, _, a) => $"({c.Reference(c.Children(p).ElementAtOrDefault(ChildInt(a, 0)) ?? Null)},)",
        ["GetChildren"] = static (c, p, _, _) => $"(@a(so) [{string.Join(",", c.Children(p).Select(c.Reference))}],)",
        ["GetIndexInParent"] = static (c, p, e, _) => $"({(p == Root ? -1 : c.Children(c.ParentPath(e)).IndexOf(p))},)",
        ["GetRelationSet"] = static (c, _, e, _) => $"(@a(ua(so)) [{c.Relations(e)}],)",
        ["GetRole"] = static (_, _, e, _) => $"(uint32 {Role(e.Role)},)",
        ["GetRoleName"] = static (_, _, e, _) => $"({Quote(RoleName(e.Role))},)",
        ["GetLocalizedRoleName"] = static (_, _, e, _) => $"({Quote(RoleName(e.Role))},)",
        ["GetState"] = static (_, _, e, _) => $"({State(e)},)",
        ["GetAttributes"] = static (_, _, e, _) => Attributes(e),
        ["GetApplication"] = static (c, _, _, _) => $"({c.Reference(Root)},)",
        ["GetInterfaces"] = static (_, p, _, _) => $"({InterfaceArray(p)},)"
    };

    string AccessibleMethod(string path, AccessibilityElement element, string method, nint parameters) =>
        AccessibleMethods[method](this, path, element, parameters);

    static string Attributes(AccessibilityElement element) =>
        $"(@a{{ss}} {{'guinevere-role': {Quote(element.Role)}, 'id': {Quote(element.Id)}, "
        + $"'virtualized': {Quote(element.Virtualized ? "true" : "false")}}},)";

    static string InterfaceArray(string path) =>
        $"@as [{string.Join(",", (path == Root ? [.. Interfaces, Prefix + "Application"] : Interfaces).Select(Quote))}]";

    string Relations(AccessibilityElement element)
    {
        var relations = new List<string>();
        AddRelation(relations, 7, element.ParentId);
        AddRelation(relations, 11, element.SourceId);
        AddRelation(relations, 10, element.TargetId);
        return string.Join(",", relations);
    }

    void AddRelation(List<string> relations, uint kind, string? id)
    {
        if (id is not null && _elements.ContainsKey(Path(id)))
        {
            relations.Add($"(uint32 {kind}, [{Reference(Path(id))}])");
        }
    }

    static IReadOnlyList<string> Actions(AccessibilityElement element) =>
        element.State.HasFlag(ControlVisualState.Disabled) ? [] : element.Actions ?? [];

    string ActionMethod(AccessibilityElement element, string method, nint parameters)
    {
        var actions = Actions(element);
        if (method == "GetActions")
        {
            return $"(@a(sss) [{string.Join(",", actions.Select(static a => $"({Quote(a)}, {Quote(a)}, '')"))}],)";
        }
        var name = ActionAt(actions, ChildInt(parameters, 0));
        if (method == "DoAction")
        {
            return Queue(element, name) ? "(true,)" : "(false,)";
        }
        return ActionDescription(method, name);
    }

    static string? ActionAt(IReadOnlyList<string> actions, int index) =>
        index >= 0 && index < actions.Count ? actions[index] : null;

    static string ActionDescription(string method, string? name) =>
        $"({Quote(method == "GetKeyBinding" ? "" : name ?? "")},)";

    bool Queue(AccessibilityElement element, string? action)
    {
        if (action is null || !Actions(element).Contains(action))
        {
            return false;
        }
        _enqueue(element.ScopeId ?? "", new AccessibilityAction(action, element.ScopeId is null ? element.Id : element.Key));
        return true;
    }

    static readonly Dictionary<string, Handler> ComponentMethods = new(StringComparer.Ordinal)
    {
        ["GetSize"] = static (_, _, e, _) => $"({(int)e.Bounds.W}, {(int)e.Bounds.H})",
        ["GetLayer"] = static (_, _, _, _) => "(uint32 3,)",
        ["GetMDIZOrder"] = static (_, _, _, _) => "(int16 0,)",
        ["GetAlpha"] = static (_, _, _, _) => "(1.0,)",
        ["GrabFocus"] = static (c, _, e, _) => Boolean(c.Queue(e, "Focus")),
        ["ScrollTo"] = static (c, _, e, _) => Boolean(c.Queue(e, "Focus")),
        ["ScrollToPoint"] = static (c, _, e, _) => Boolean(c.Queue(e, "Focus")),
        ["GetExtents"] = static (c, _, e, a) => c.Extents(e, a),
        ["GetPosition"] = static (c, _, e, a) => c.Position(e, a),
        ["Contains"] = static (c, _, e, a) => Boolean(ContainsPoint(e, c.Point(a))),
        ["GetAccessibleAtPoint"] = static (c, p, _, a) => $"({c.Reference(c.HitTest(p, c.Point(a)))},)"
    };

    string ComponentMethod(string path, AccessibilityElement element, string method, nint parameters) =>
        ComponentMethods.TryGetValue(method, out var handler) ? handler(this, path, element, parameters) : "(false,)";

    static string Boolean(bool value) => value ? "(true,)" : "(false,)";

    Vector2 CoordinateOrigin(nint parameters, nuint index)
    {
        var child = g_variant_get_child_value(parameters, index);
        var coordinate = g_variant_get_uint32(child);
        g_variant_unref(child);
        return coordinate == 0 ? _origin : Vector2.Zero;
    }

    Vector2 Point(nint parameters) => new Vector2(ChildInt(parameters, 0), ChildInt(parameters, 1))
        - CoordinateOrigin(parameters, 2);

    string Extents(AccessibilityElement element, nint parameters)
    {
        var bounds = element.Bounds;
        var origin = CoordinateOrigin(parameters, 0);
        return $"(({(int)(bounds.X + origin.X)}, {(int)(bounds.Y + origin.Y)}, {(int)bounds.W}, {(int)bounds.H}),)";
    }

    string Position(AccessibilityElement element, nint parameters)
    {
        var point = element.Bounds.Position + CoordinateOrigin(parameters, 0);
        return $"({(int)point.X}, {(int)point.Y})";
    }

    static bool ContainsPoint(AccessibilityElement element, Vector2 point) =>
        !element.Virtualized && element.Bounds.Contains(point);

    string HitTest(string path, Vector2 point) => Children(path).LastOrDefault(child => ContainsPoint(_elements[child], point)) ?? Null;

    static string State(AccessibilityElement element)
    {
        var bits = 1UL << 11 | 1UL << 30;
        if (!element.State.HasFlag(ControlVisualState.Disabled))
        {
            bits |= 1UL << 8 | 1UL << 24;
        }
        if (!element.Virtualized)
        {
            bits |= 1UL << 25;
        }
        if (element.State.HasFlag(ControlVisualState.Focused))
        {
            bits |= 1UL << 12;
        }
        if (element.State.HasFlag(ControlVisualState.Selected))
        {
            bits |= 1UL << 22 | 1UL << 23;
        }
        if (element.State.HasFlag(ControlVisualState.Checked))
        {
            bits |= 1UL << 4;
        }
        if (element.Expanded is { } expanded)
        {
            bits |= 1UL << 9 | 1UL << (expanded ? 10 : 5);
        }
        return $"[uint32 {(uint)bits}, uint32 {(uint)(bits >> 32)}]";
    }

    static readonly Dictionary<string, (uint Value, string Name)> Roles = new(StringComparer.Ordinal)
    {
        ["Application"] = (75, "application"),
        ["Window"] = (23, "frame"),
        ["Graph"] = (6, "canvas"),
        ["Node"] = (39, "panel"),
        ["Group"] = (99, "grouping"),
        ["Port"] = (43, "push button"),
        ["Edge"] = (88, "link"),
        ["Button"] = (43, "push button"),
        ["Checkbox"] = (7, "check box"),
        ["Switch"] = (7, "check box"),
        ["Radio"] = (44, "radio button"),
        ["Tab"] = (37, "page tab"),
        ["Slider"] = (51, "slider"),
        ["ComboBox"] = (11, "combo box"),
        ["Menu"] = (33, "menu"),
        ["MenuItem"] = (35, "menu item"),
        ["TextInput"] = (79, "entry")
    };

    static uint Role(string role) => Roles.TryGetValue(role, out var value) ? value.Value : 39;
    static string RoleName(string role) => Roles.TryGetValue(role, out var value) ? value.Name : "panel";
}
