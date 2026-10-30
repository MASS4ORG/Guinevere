using System.Runtime.InteropServices;

namespace Guinevere;

static class AtSpiNative
{
    const string Gio = "libgio-2.0.so.0";
    const string Glib = "libglib-2.0.so.0";
    const string Gobject = "libgobject-2.0.so.0";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Method(nint connection, nint sender, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string iface, [MarshalAs(UnmanagedType.LPUTF8Str)] string method,
        nint parameters, nint invocation, nint data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint GetProperty(nint connection, nint sender, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string iface, [MarshalAs(UnmanagedType.LPUTF8Str)] string property,
        nint error, nint data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int SetProperty(nint connection, nint sender, nint path, nint iface, nint property,
        nint value, nint error, nint data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void Ready(nint source, nint result, nint data);

    [DllImport(Gio)] internal static extern nint g_bus_get_sync(int type, nint cancel, out nint error);
    [DllImport(Gio)]
    internal static extern nint g_dbus_connection_new_for_address_sync(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string address, int flags, nint observer, nint cancel, out nint error);
    [DllImport(Gio)] internal static extern nint g_dbus_connection_get_unique_name(nint connection);
    [DllImport(Gio)]
    internal static extern nint g_dbus_connection_call_sync(nint connection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string bus, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string iface, [MarshalAs(UnmanagedType.LPUTF8Str)] string method,
        nint parameters, nint replyType, int flags, int timeout, nint cancel, out nint error);
    [DllImport(Gio)]
    internal static extern void g_dbus_connection_call(nint connection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string bus, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string iface, [MarshalAs(UnmanagedType.LPUTF8Str)] string method,
        nint parameters, nint replyType, int flags, int timeout, nint cancel, Ready callback, nint data);
    [DllImport(Gio)] internal static extern nint g_dbus_connection_call_finish(nint connection, nint result, out nint error);
    [DllImport(Gio)] internal static extern int g_dbus_connection_close_sync(nint connection, nint cancel, out nint error);
    [DllImport(Gio)]
    internal static extern uint g_dbus_connection_register_object(nint connection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path, nint info, nint vtable, nint data, nint destroy, out nint error);
    [DllImport(Gio)] internal static extern int g_dbus_connection_unregister_object(nint connection, uint id);
    [DllImport(Gio)]
    internal static extern nint g_dbus_node_info_new_for_xml(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string xml, out nint error);
    [DllImport(Gio)]
    internal static extern nint g_dbus_node_info_lookup_interface(nint info,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Gio)] internal static extern void g_dbus_node_info_unref(nint info);
    [DllImport(Gio)] internal static extern void g_dbus_method_invocation_return_value(nint invocation, nint value);
    [DllImport(Gio)]
    internal static extern void g_dbus_method_invocation_return_dbus_error(nint invocation,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string message);
    [DllImport(Gio)]
    internal static extern int g_dbus_connection_emit_signal(nint connection, nint destination,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string iface,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string signal, nint parameters, out nint error);
    [DllImport(Glib)] internal static extern nint g_main_context_new();
    [DllImport(Glib)] internal static extern void g_main_context_push_thread_default(nint context);
    [DllImport(Glib)] internal static extern void g_main_context_pop_thread_default(nint context);
    [DllImport(Glib)] internal static extern int g_main_context_iteration(nint context, int block);
    [DllImport(Glib)] internal static extern void g_main_context_wakeup(nint context);
    [DllImport(Glib)] internal static extern void g_main_context_unref(nint context);
    [DllImport(Glib)]
    internal static extern nint g_variant_parse(nint type,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text, nint limit, nint end, out nint error);
    [DllImport(Glib)] internal static extern nint g_variant_get_child_value(nint value, nuint index);
    [DllImport(Glib)] internal static extern int g_variant_get_int32(nint value);
    [DllImport(Glib)] internal static extern uint g_variant_get_uint32(nint value);
    [DllImport(Glib)] internal static extern nint g_variant_get_string(nint value, out nuint length);
    [DllImport(Glib)] internal static extern void g_variant_unref(nint value);
    [DllImport(Glib)] internal static extern nint g_variant_ref_sink(nint value);
    [DllImport(Glib)] internal static extern void g_error_free(nint error);
    [DllImport(Gobject)] internal static extern void g_object_unref(nint value);

    internal static string Utf8(nint value) => Marshal.PtrToStringUTF8(value) ?? "";

    internal static void Check(nint error)
    {
        if (error == 0)
        {
            return;
        }
        var message = Utf8(Marshal.ReadIntPtr(error, 8));
        g_error_free(error);
        throw new InvalidOperationException(message);
    }

    internal static nint Variant(string text)
    {
        var value = g_variant_parse(0, text, 0, 0, out var error);
        Check(error);
        return value;
    }

    internal static string ChildString(nint value, nuint index)
    {
        var child = g_variant_get_child_value(value, index);
        try
        {
            return Utf8(g_variant_get_string(child, out _));
        }
        finally
        {
            g_variant_unref(child);
        }
    }

    internal static int ChildInt(nint value, nuint index)
    {
        var child = g_variant_get_child_value(value, index);
        try
        {
            return g_variant_get_int32(child);
        }
        finally
        {
            g_variant_unref(child);
        }
    }
}
