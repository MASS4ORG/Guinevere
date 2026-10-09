using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Platform;

/// <summary>
/// Covers the system appearance readers per platform, the monitor's change notification, and the per-frame
/// <see cref="Gui.SystemAppearance"/> snapshot, including platforms without the capability.
/// </summary>
public class SystemAppearanceTests
{
    const string PortalDark =
        "({'org.freedesktop.appearance': {'contrast': <uint32 1>, 'color-scheme': <uint32 1>, " +
        "'reduced-motion': <uint32 0>, 'accent-color': <(0.23921568691730499, 0.68235296010971069, " +
        "0.91372549533843994)>}},)";

    /// <summary>The portal's scheme, contrast and accent parse from <c>gdbus</c> output.</summary>
    [Fact]
    public void ParsesThePortal()
    {
        var appearance = SystemAppearanceReaders.ParsePortal(PortalDark);

        Assert.Equal(ColorScheme.Dark, appearance.ColorScheme);
        Assert.Equal(ContrastPreference.More, appearance.Contrast);
        Assert.Equal(Color.ParseHex("#3daee9"), appearance.AccentColor);
    }

    /// <summary>Light, unset, out-of-range accents and missing output degrade field by field.</summary>
    [Theory]
    [InlineData("({'org.freedesktop.appearance': {'color-scheme': <uint32 2>}},)", ColorScheme.Light)]
    [InlineData("({'org.freedesktop.appearance': {'color-scheme': <uint32 0>, 'accent-color': <(-1.0, 0.0, 0.0)>}},)",
        ColorScheme.NoPreference)]
    [InlineData("({'org.freedesktop.appearance': {}},)", ColorScheme.NoPreference)]
    [InlineData("", ColorScheme.NoPreference)]
    [InlineData(null, ColorScheme.NoPreference)]
    public void PortalDegradesToNoPreference(string? output, ColorScheme scheme)
    {
        var appearance = SystemAppearanceReaders.ParsePortal(output);

        Assert.Equal(scheme, appearance.ColorScheme);
        Assert.Null(appearance.AccentColor);
        Assert.Equal(ContrastPreference.NoPreference, appearance.Contrast);
    }

    /// <summary>Only appearance-namespace SettingChanged signals trigger a refresh.</summary>
    [Theory]
    [InlineData("/org/freedesktop/portal/desktop: org.freedesktop.portal.Settings.SettingChanged " +
                "('org.freedesktop.appearance', 'color-scheme', <uint32 1>)", true)]
    [InlineData("/org/freedesktop/portal/desktop: org.freedesktop.portal.Settings.SettingChanged " +
                "('org.kde.VirtualKeyboard', 'active', <true>)", false)]
    [InlineData("The name org.freedesktop.portal.Desktop is owned by :1.42", false)]
    [InlineData(null, false)]
    public void RecognizesAppearanceSignals(string? line, bool expected) =>
        Assert.Equal(expected, SystemAppearanceReaders.IsPortalAppearanceSignal(line));

    /// <summary>Windows registry values map to scheme, ABGR accent and the high-contrast flag.</summary>
    [Fact]
    public void MapsWindowsRegistryValues()
    {
        var dark = SystemAppearanceReaders.FromWindows(0, unchecked((int)0xffd77800), 0x7f);
        Assert.Equal(new SystemAppearance(ColorScheme.Dark, Color.ParseHex("#0078d7"), ContrastPreference.More), dark);

        var light = SystemAppearanceReaders.FromWindows(1, null, 0x7e);
        Assert.Equal(new SystemAppearance(ColorScheme.Light), light);
        Assert.Equal(SystemAppearance.NoPreference, SystemAppearanceReaders.FromWindows(null, null, null));
    }

    /// <summary>The registry reader asks for the documented keys and values.</summary>
    [Fact]
    public void ReadsTheDocumentedRegistryValues()
    {
        var asked = new List<string>();
        var appearance = SystemAppearanceReaders.FromRegistry((key, name) =>
        {
            asked.Add($@"{key}\{name}");
            return name switch { "AppsUseLightTheme" => 0, "AccentColor" => unchecked((int)0xff0000ff), _ => "1" };
        });

        Assert.Equal(new SystemAppearance(ColorScheme.Dark, Color.Red, ContrastPreference.More), appearance);
        Assert.Contains(asked, path => path.EndsWith(@"Themes\Personalize\AppsUseLightTheme", StringComparison.Ordinal));
        Assert.Contains(asked, path => path.EndsWith(@"DWM\AccentColor", StringComparison.Ordinal));
        Assert.Contains(asked, path => path.EndsWith(@"HighContrast\Flags", StringComparison.Ordinal));
    }

    /// <summary>macOS defaults map to dark/light and the accent palette, blue when unset.</summary>
    [Theory]
    [InlineData("Dark\n", "0\n", ColorScheme.Dark, "#e0383eff")]
    [InlineData(null, null, ColorScheme.Light, "#007affff")]
    [InlineData(null, "-1", ColorScheme.Light, "#989898ff")]
    [InlineData("Light", "6", ColorScheme.Light, "#f74f9eff")]
    [InlineData(null, "1", ColorScheme.Light, "#f7821bff")]
    [InlineData(null, "2", ColorScheme.Light, "#ffc600ff")]
    [InlineData(null, "3", ColorScheme.Light, "#62ba46ff")]
    [InlineData(null, "5", ColorScheme.Light, "#953d96ff")]
    public void MapsMacOsDefaults(string? style, string? accent, ColorScheme scheme, string hex)
    {
        var appearance = SystemAppearanceReaders.FromMacOs(style, accent);
        Assert.Equal(scheme, appearance.ColorScheme);
        Assert.Equal(hex, appearance.AccentColor?.ToHex());
    }

    /// <summary>The monitor raises Changed only when a refresh reads a different appearance.</summary>
    [Fact]
    public void MonitorRaisesChangedOnDifferences()
    {
        var current = new SystemAppearance(ColorScheme.Light);
        using var monitor = new SystemAppearanceMonitor(() => current);
        var raised = new List<SystemAppearance>();
        monitor.Changed += raised.Add;

        monitor.Refresh();
        current = new SystemAppearance(ColorScheme.Dark, Color.Red);
        monitor.Refresh();

        Assert.Equal([current], raised);
        Assert.Equal(current, monitor.Appearance);
    }

    /// <summary>A failing reader counts as no preference instead of throwing.</summary>
    [Fact]
    public void MonitorSwallowsReaderFailures()
    {
        using var monitor = new SystemAppearanceMonitor(() => throw new InvalidOperationException());
        Assert.Equal(SystemAppearance.NoPreference, monitor.Appearance);
    }

    /// <summary>A polling monitor picks up changes by itself.</summary>
    [Fact]
    public async Task MonitorPolls()
    {
        var current = SystemAppearance.NoPreference;
        using var monitor = new SystemAppearanceMonitor(() => current, TimeSpan.FromMilliseconds(10));
        var changed = new TaskCompletionSource<SystemAppearance>();
        monitor.Changed += value => changed.TrySetResult(value);
        current = new SystemAppearance(ColorScheme.Dark);

        Assert.Equal(current, await changed.Task.WaitAsync(TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));
    }

    /// <summary>The platform monitor starts, reads and stops without throwing wherever the tests run.</summary>
    [Fact]
    public void PlatformMonitorStartsAndStops()
    {
        var monitor = SystemAppearanceMonitor.Create();
        _ = monitor.Appearance;
        monitor.Dispose();
        monitor.Dispose();
    }

    /// <summary>The GUI snapshots the appearance once per frame and flags the frame it changed.</summary>
    [Fact]
    public void GuiSnapshotsTheAppearancePerFrame()
    {
        using var harness = new FrameHarness();
        Assert.Equal(SystemAppearance.NoPreference, harness.Gui.SystemAppearance);
        harness.Frame(_ => { });
        Assert.False(harness.Gui.SystemAppearanceChanged);

        var current = new SystemAppearance(ColorScheme.Dark);
        using var monitor = new SystemAppearanceMonitor(() => current);
        harness.Gui.Platform.Register<IAppearanceCapability>(monitor);
        var seen = new List<(SystemAppearance, bool)>();
        harness.Frame(g => seen.Add((g.SystemAppearance, g.SystemAppearanceChanged)));
        harness.Frame(g => seen.Add((g.SystemAppearance, g.SystemAppearanceChanged)));

        Assert.Equal([(current, true), (current, true), (current, false), (current, false)], seen);
    }

    /// <summary>Following the system theme picks the matching sheet and adopts the accent as theme tokens.</summary>
    [Fact]
    public void ExcaliburFollowsTheSystemAppearance()
    {
        using var harness = new FrameHarness();
        var current = new SystemAppearance(ColorScheme.Light, Color.ParseHex("#ff0000"));
        using var monitor = new SystemAppearanceMonitor(() => current);
        harness.Gui.Platform.Register<IAppearanceCapability>(monitor);
        Color accent = default;
        harness.Frame(g =>
        {
            ExcaliburStyles.FollowSystemAppearance(g);
            accent = g.ResolveStyle("button", ["primary"]).GetColor("background-color") ?? default;
        });

        Assert.Contains(ExcaliburStyles.Light, harness.Gui.StyleSheets);
        Assert.Equal(Color.ParseHex("#ff0000"), accent);

        current = SystemAppearance.NoPreference;
        monitor.Refresh();
        harness.Frame(g => ExcaliburStyles.FollowSystemAppearance(g));
        Assert.DoesNotContain(ExcaliburStyles.Light, harness.Gui.StyleSheets);
    }
}
