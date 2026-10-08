using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// The default <c>.pss</c> sheet that gives the Excalibur controls their look. It is kept as the lowest-priority sheet
/// of every GUI that draws a control, so application sheets and <c>gui.SetStyleToken</c> override any of it.
/// </summary>
public static class ExcaliburStyles
{
    const string ResourceName = "Guinevere.Excalibur.guinevere.default.pss";

    static readonly Lazy<string> Text = new(LoadText);
    static readonly Lazy<StyleSheet> Sheet = new(() =>
        StyleSheet.Parse(Text.Value, new StyleSheetOptions { SourceName = "guinevere.default.pss" }));
    static readonly ConditionalWeakTable<Gui, ControlPalette> Bridged = new();

    /// <summary>The default sheet's source, for applications that copy it as the start of their own theme.</summary>
    public static string DefaultSheetText => Text.Value;

    /// <summary>The parsed default sheet, shared by every GUI.</summary>
    public static StyleSheet DefaultSheet => Sheet.Value;

    /// <summary>
    /// Puts the default sheet first in the GUI's sheets when it is missing or was moved, and forwards a custom
    /// <see cref="Gui.ControlPalette"/> to the sheet tokens it corresponds to.
    /// </summary>
    internal static void Ensure(Gui gui)
    {
        var sheets = gui.StyleSheets;
        if (sheets.Count == 0 || !ReferenceEquals(sheets[0], DefaultSheet))
        {
            sheets.Remove(DefaultSheet);
            sheets.Insert(0, DefaultSheet);
        }
        BridgePalette(gui);
    }

    /// <summary>
    /// Applies a palette set through <see cref="Gui.ControlPalette"/> as host tokens, once per palette instance. The
    /// built-in light palette is what the default sheet already holds, so it needs no tokens.
    /// </summary>
    static void BridgePalette(Gui gui)
    {
        var palette = gui.ControlPalette;
        var known = Bridged.TryGetValue(gui, out var applied);
        if (known && ReferenceEquals(applied, palette)) return;
        Bridged.AddOrUpdate(gui, palette);
        if (!known && ReferenceEquals(palette, ControlPalette.Light)) return;
        foreach (var (name, color) in Tokens(palette)) gui.StyleSheets.SetToken(name, StyleValue.Format(color));
    }

    static IEnumerable<(string Name, Color Color)> Tokens(ControlPalette palette) =>
    [
        ("base-background", palette.BaseBackground), ("surface", palette.Surface),
        ("surface-hover", palette.SurfaceHover), ("surface-active", palette.SurfaceActive), ("popup", palette.Popup),
        ("border", palette.Border), ("border-active", palette.BorderActive), ("divider", palette.Divider),
        ("accent", palette.Accent), ("accent-hover", palette.AccentHover), ("accent-subtle", palette.AccentSubtle),
        ("text", palette.Text), ("text-dim", palette.TextDim), ("text-disabled", palette.TextDisabled),
        ("text-on-accent", palette.TextOnAccent), ("selected", palette.Selected), ("positive", palette.Positive),
        ("negative", palette.Negative), ("warning", palette.Warning), ("info", palette.Info),
        ("focus-ring", palette.FocusRing), ("shadow", palette.Shadow), ("overlay", palette.Overlay),
        ("text-selection", palette.TextSelection),
    ];

    static string LoadText()
    {
        using var stream = typeof(ExcaliburStyles).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
