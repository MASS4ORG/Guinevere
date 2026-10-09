using System.Diagnostics;

namespace Guinevere;

/// <summary>
/// The default <see cref="IAppearanceCapability"/> for desktop integrations. It reads the appearance when created,
/// then follows changes: Linux listens to the XDG desktop portal's <c>SettingChanged</c> signal, and other platforms
/// poll. Unsupported platforms, or a missing portal, report <see cref="SystemAppearance.NoPreference"/>.
/// </summary>
public sealed class SystemAppearanceMonitor : IAppearanceCapability, IDisposable
{
    static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);

    readonly Func<SystemAppearance> _read;
    readonly Lock _gate = new();
    SystemAppearance _appearance;
    Timer? _poll;
    Process? _watch;

    /// <summary>Creates a monitor over a custom reader, refreshed every <paramref name="pollInterval"/> when given.</summary>
    /// <param name="read">Returns the current appearance; exceptions count as no preference.</param>
    /// <param name="pollInterval">How often to re-read, or <c>null</c> to refresh only through <see cref="Refresh"/>.</param>
    public SystemAppearanceMonitor(Func<SystemAppearance> read, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        _read = read;
        _appearance = SafeRead();
        if (pollInterval is { } interval) _poll = new Timer(_ => Refresh(), null, interval, interval);
    }

    /// <summary>Creates the monitor for the current operating system.</summary>
    public static SystemAppearanceMonitor Create()
    {
        var poll = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
        var monitor = new SystemAppearanceMonitor(SystemAppearanceReaders.ReadCurrent, poll ? DefaultPollInterval : null);
        if (SystemAppearanceReaders.UsesPortal) monitor.WatchPortal();
        return monitor;
    }

    /// <inheritdoc />
    public SystemAppearance Appearance
    {
        get
        {
            lock (_gate) return _appearance;
        }
    }

    /// <inheritdoc />
    public event Action<SystemAppearance>? Changed;

    /// <summary>Re-reads the appearance and raises <see cref="Changed"/> when it differs.</summary>
    public void Refresh()
    {
        var next = SafeRead();
        lock (_gate)
        {
            if (next == _appearance) return;
            _appearance = next;
        }
        Changed?.Invoke(next);
    }

    /// <summary>Stops following changes.</summary>
    public void Dispose()
    {
        _poll?.Dispose();
        _poll = null;
        if (_watch is { } watch)
        {
            _watch = null;
            try
            {
                if (!watch.HasExited) watch.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            watch.Dispose();
        }
    }

    SystemAppearance SafeRead()
    {
        try
        {
            return _read();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return SystemAppearance.NoPreference;
        }
    }

    /// <summary>Follows the portal's signals through <c>gdbus monitor</c>; without it, the first read stays.</summary>
    void WatchPortal()
    {
        var watch = new Process
        {
            StartInfo = SystemAppearanceReaders.Command("gdbus", SystemAppearanceReaders.PortalMonitorArguments)
        };
        watch.OutputDataReceived += (_, line) =>
        {
            if (SystemAppearanceReaders.IsPortalAppearanceSignal(line.Data)) Refresh();
        };
        watch.ErrorDataReceived += (_, _) => { };
        try
        {
            if (watch.Start())
            {
                watch.BeginOutputReadLine();
                watch.BeginErrorReadLine();
                _watch = watch;
                return;
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
                                              or InvalidOperationException)
        {
        }
        watch.Dispose();
    }
}
