using System.Runtime.InteropServices;
using static Guinevere.AtSpiNative;

namespace Guinevere.Tests.Platform;

sealed class AtSpiClient : IDisposable
{
    readonly nint _connection;
    readonly string _bus;

    internal AtSpiClient(string bus)
    {
        _bus = bus;
        var session = g_bus_get_sync(2, 0, out var error);
        Check(error);
        var reply = g_dbus_connection_call_sync(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus",
            "GetAddress", 0, 0, 0, 2000, 0, out error);
        Check(error);
        var address = ChildString(reply, 0);
        g_variant_unref(reply);
        g_object_unref(session);
        _connection = g_dbus_connection_new_for_address_sync(address, 9, 0, 0, out error);
        Check(error);
    }

    internal string Call(string path, string iface, string method, string? parameters = null)
    {
        var args = parameters is null ? 0 : Variant(parameters);
        try
        {
            var reply = g_dbus_connection_call_sync(_connection, _bus, path, iface, method, args, 0, 0, 2000, 0,
                out var error);
            Check(error);
            var printed = g_variant_print(reply, 1);
            var text = Utf8(printed);
            g_free(printed);
            g_variant_unref(reply);
            return text;
        }
        finally
        {
            if (args != 0)
            {
                g_variant_unref(args);
            }
        }
    }

    internal string Property(string path, string iface, string name) =>
        Call(path, "org.freedesktop.DBus.Properties", "Get", $"('org.a11y.atspi.{iface}', '{name}')");

    internal string Accessible(string path, string method, string? parameters = null) =>
        Call(path, "org.a11y.atspi.Accessible", method, parameters);

    public void Dispose()
    {
        g_dbus_connection_close_sync(_connection, 0, out var error);
        Check(error);
        g_object_unref(_connection);
    }

    [DllImport("libglib-2.0.so.0")] static extern nint g_variant_print(nint value, int annotate);
    [DllImport("libglib-2.0.so.0")] static extern void g_free(nint value);
}
