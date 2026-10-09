using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// The default <c>.pss</c> sheet that gives the Excalibur controls their look, and the built-in color themes layered
/// over it. The default sheet is kept as the lowest-priority sheet of every GUI that draws a control, so themes,
/// application sheets and <c>gui.SetStyleToken</c> override any of it.
/// </summary>
public static class ExcaliburStyles
{
    const string ResourcePrefix = "Guinevere.Excalibur.";

    static readonly Lazy<string> Text = new(() => LoadText("guinevere.default.pss"));
    static readonly Lazy<StyleSheet> Sheet = new(() => Parse("guinevere.default.pss", Text.Value));
    static readonly Lazy<StyleSheet> LightSheet = new(() => Load("guinevere.light.pss"));
    static readonly Lazy<StyleSheet> MonoLightSheet = new(() => Load("guinevere.mono-light.pss"));
    static readonly Lazy<StyleSheet> MonoDarkSheet = new(() => Load("guinevere.mono-dark.pss"));
    static readonly ConditionalWeakTable<Gui, StyleSheet> Themes = new();

    /// <summary>The default sheet's source, for applications that copy it as the start of their own theme.</summary>
    public static string DefaultSheetText => Text.Value;

    /// <summary>The parsed default sheet, shared by every GUI.</summary>
    public static StyleSheet DefaultSheet => Sheet.Value;

    /// <summary>The default dark look; the default sheet itself.</summary>
    public static StyleSheet Dark => Sheet.Value;

    /// <summary>A light color theme.</summary>
    public static StyleSheet Light => LightSheet.Value;

    /// <summary>A monochrome light color theme.</summary>
    public static StyleSheet MonoLight => MonoLightSheet.Value;

    /// <summary>A monochrome dark color theme.</summary>
    public static StyleSheet MonoDark => MonoDarkSheet.Value;

    /// <summary>Puts the default sheet first in the GUI's sheets when it is missing or was moved.</summary>
    public static void Ensure(Gui gui)
    {
        var sheets = gui.StyleSheets;
        if (sheets.Count != 0 && ReferenceEquals(sheets[0], DefaultSheet)) return;
        sheets.Remove(DefaultSheet);
        sheets.Insert(0, DefaultSheet);
    }

    /// <summary>
    /// Layers a color theme right above the default sheet, replacing the theme set before; <see cref="Dark"/> or
    /// <c>null</c> restores the default look. Application sheets added after it still override it.
    /// </summary>
    /// <param name="gui">The GUI to theme.</param>
    /// <param name="theme">A token sheet such as <see cref="Light"/>, or any sheet of the application's own.</param>
    public static void SetTheme(Gui gui, StyleSheet? theme)
    {
        ArgumentNullException.ThrowIfNull(gui);
        Ensure(gui);
        var sheets = gui.StyleSheets;
        if (Themes.TryGetValue(gui, out var current))
        {
            if (ReferenceEquals(current, theme)) return;
            sheets.Remove(current);
            Themes.Remove(gui);
        }
        if (theme is null || ReferenceEquals(theme, DefaultSheet)) return;
        sheets.Insert(1, theme);
        Themes.AddOrUpdate(gui, theme);
    }

    /// <summary>
    /// A color token of the active sheets, such as <c>text</c> or <c>surface-hover</c>, for drawing that should follow
    /// the theme; transparent when no sheet defines it.
    /// </summary>
    /// <param name="gui">The GUI whose sheets define the token.</param>
    /// <param name="name">Token name, with or without the <c>$</c> or <c>--</c> prefix.</param>
    public static Color TokenColor(Gui gui, string name)
    {
        Ensure(gui);
        return gui.StyleSheets.GetTokenColor(name) ?? Color.Transparent;
    }

    /// <summary>Drop feedback in the theme's <c>$positive</c> and <c>$negative</c> colors.</summary>
    /// <param name="gui">The GUI whose sheets supply the colors.</param>
    public static DropIndicatorStyle DroppableArea(Gui gui) =>
        new(gui.StyleSheets.GetTokenColor("--positive"), gui.StyleSheets.GetTokenColor("--negative"));

    static StyleSheet Load(string name) => Parse(name, LoadText(name));

    static StyleSheet Parse(string name, string text) =>
        StyleSheet.Parse(text, new StyleSheetOptions { SourceName = name });

    static string LoadText(string name)
    {
        using var stream = typeof(ExcaliburStyles).Assembly.GetManifestResourceStream(ResourcePrefix + name)
                           ?? throw new InvalidOperationException($"Missing embedded resource {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
