using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>
/// Reads the system appearance per platform: the XDG desktop portal through <c>gdbus</c> on Linux, the registry on
/// Windows and <c>defaults</c> on macOS. Every reader degrades to <see cref="SystemAppearance.NoPreference"/>.
/// </summary>
static partial class SystemAppearanceReaders
{
    internal const string PortalNamespace = "org.freedesktop.appearance";
    static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The <c>gdbus</c> arguments that print the portal's appearance settings.</summary>
    internal static readonly string[] PortalReadArguments =
    [
        "call", "--session", "--dest", "org.freedesktop.portal.Desktop",
        "--object-path", "/org/freedesktop/portal/desktop",
        "--method", "org.freedesktop.portal.Settings.ReadAll", $"['{PortalNamespace}']"
    ];

    /// <summary>The <c>gdbus</c> arguments that stream the portal's signals, one per line.</summary>
    internal static readonly string[] PortalMonitorArguments =
    [
        "monitor", "--session", "--dest", "org.freedesktop.portal.Desktop",
        "--object-path", "/org/freedesktop/portal/desktop"
    ];

    [GeneratedRegex(@"'color-scheme': <uint32 (\d+)>")]
    private static partial Regex PortalColorScheme();

    [GeneratedRegex(@"'contrast': <uint32 (\d+)>")]
    private static partial Regex PortalContrast();

    [GeneratedRegex(@"'accent-color': <\(([^,()]+), ([^,()]+), ([^,()]+)\)>")]
    private static partial Regex PortalAccent();

    const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    const string DwmKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM";
    const string HighContrastKey = @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast";

    /// <summary>Whether the current platform reports its appearance through the XDG desktop portal.</summary>
    internal static bool UsesPortal => OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD();

    /// <summary>Reads the current platform's appearance.</summary>
    internal static SystemAppearance ReadCurrent() =>
        OperatingSystem.IsWindows() ? FromRegistry(ReadRegistry)
        : OperatingSystem.IsMacOS() ? ReadMacOs()
        : UsesPortal ? ParsePortal(Run("gdbus", PortalReadArguments))
        : SystemAppearance.NoPreference;

    /// <summary>Whether a <c>gdbus monitor</c> line reports a change to the appearance settings.</summary>
    internal static bool IsPortalAppearanceSignal(string? line) =>
        line is not null && line.Contains("SettingChanged", StringComparison.Ordinal)
                         && line.Contains($"'{PortalNamespace}'", StringComparison.Ordinal);

    /// <summary>
    /// Parses <c>Settings.ReadAll</c> output: <c>color-scheme</c> 1 is dark and 2 light, <c>contrast</c> 1 is high, and
    /// an <c>accent-color</c> channel outside [0, 1] means no accent.
    /// </summary>
    internal static SystemAppearance ParsePortal(string? output)
    {
        if (string.IsNullOrEmpty(output)) return SystemAppearance.NoPreference;
        var contrast = PortalUint(PortalContrast(), output) == "1"
            ? ContrastPreference.More
            : ContrastPreference.NoPreference;
        return new SystemAppearance(PortalScheme(output), ParsePortalAccent(output), contrast);
    }

    static ColorScheme PortalScheme(string output) => PortalUint(PortalColorScheme(), output) switch
    {
        "1" => ColorScheme.Dark,
        "2" => ColorScheme.Light,
        _ => ColorScheme.NoPreference
    };

    static string? PortalUint(Regex pattern, string output) =>
        pattern.Match(output) is { Success: true } match ? match.Groups[1].Value : null;

    static Color? ParsePortalAccent(string output)
    {
        var match = PortalAccent().Match(output);
        if (!match.Success) return null;
        Span<byte> channels = stackalloc byte[3];
        for (var i = 0; i < 3; i++)
        {
            if (!double.TryParse(match.Groups[i + 1].Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var value) || value is < 0 or > 1)
                return null;
            channels[i] = (byte)Math.Round(value * 255);
        }
        return Color.FromArgb(255, channels[0], channels[1], channels[2]);
    }

    /// <summary>
    /// Maps the registry values: <c>AppsUseLightTheme</c> (0 dark, 1 light), DWM <c>AccentColor</c> packed as
    /// <c>0xAABBGGRR</c>, and the high-contrast <c>Flags</c> whose bit 0 means on.
    /// </summary>
    internal static SystemAppearance FromWindows(int? appsUseLightTheme, int? accentAbgr, int? highContrastFlags)
    {
        var scheme = appsUseLightTheme switch
        {
            0 => ColorScheme.Dark,
            1 => ColorScheme.Light,
            _ => ColorScheme.NoPreference
        };
        Color? accent = accentAbgr is { } abgr
            ? Color.FromArgb(255, abgr & 0xff, (abgr >> 8) & 0xff, (abgr >> 16) & 0xff)
            : null;
        var contrast = (highContrastFlags & 1) == 1
            ? ContrastPreference.More
            : ContrastPreference.NoPreference;
        return new SystemAppearance(scheme, accent, contrast);
    }

    /// <summary>
    /// Maps <c>defaults read -g</c> output: <c>AppleInterfaceStyle</c> is "Dark" in dark mode and missing in light
    /// mode; <c>AppleAccentColor</c> is -1 (graphite) to 6 (pink) and missing for the default blue.
    /// </summary>
    internal static SystemAppearance FromMacOs(string? interfaceStyle, string? accentColor)
    {
        var scheme = interfaceStyle?.Trim() == "Dark" ? ColorScheme.Dark : ColorScheme.Light;
        var accent = int.TryParse(accentColor?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            ? index
            : 4;
        return new SystemAppearance(scheme, MacOsAccent(index: accent));
    }

    static Color MacOsAccent(int index) => index switch
    {
        -1 => Color.FromArgb(255, 0x98, 0x98, 0x98),
        0 => Color.FromArgb(255, 0xe0, 0x38, 0x3e),
        1 => Color.FromArgb(255, 0xf7, 0x82, 0x1b),
        2 => Color.FromArgb(255, 0xff, 0xc6, 0x00),
        3 => Color.FromArgb(255, 0x62, 0xba, 0x46),
        5 => Color.FromArgb(255, 0x95, 0x3d, 0x96),
        6 => Color.FromArgb(255, 0xf7, 0x4f, 0x9e),
        _ => Color.FromArgb(255, 0x00, 0x7a, 0xff)
    };

    /// <summary>Reads the Windows appearance values through <paramref name="read"/> (key path, value name).</summary>
    internal static SystemAppearance FromRegistry(Func<string, string, object?> read) =>
        FromWindows(read(PersonalizeKey, "AppsUseLightTheme") as int?, read(DwmKey, "AccentColor") as int?,
            int.TryParse(read(HighContrastKey, "Flags") as string, out var flags) ? flags : null);

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    static object? ReadRegistry(string key, string name)
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue(key, name, null);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or IOException)
        {
            return null;
        }
    }

    static SystemAppearance ReadMacOs() =>
        FromMacOs(Run("defaults", ["read", "-g", "AppleInterfaceStyle"]),
            Run("defaults", ["read", "-g", "AppleAccentColor"]));

    /// <summary>Runs a command and returns its standard output, or <c>null</c> when it is missing, fails or hangs.</summary>
    internal static string? Run(string fileName, IEnumerable<string> arguments)
    {
        try
        {
            using var process = Process.Start(Command(fileName, arguments));
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(CommandTimeout))
            {
                process.Kill();
                return null;
            }
            return process.ExitCode == 0 ? output.Result : null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A hidden, redirected start for a command-line tool.</summary>
    internal static ProcessStartInfo Command(string fileName, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }
}
