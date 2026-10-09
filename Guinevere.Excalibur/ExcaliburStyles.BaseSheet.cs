using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ExcaliburStyles
{
    static readonly ConditionalWeakTable<Gui, StyleSheet> Bases = new();

    static readonly string[] BaseColors =
    [
        "base-background", "surface", "surface-hover", "surface-active", "popup", "border", "border-active",
        "divider", "accent", "accent-hover", "accent-subtle", "text", "text-dim", "text-disabled", "text-on-accent",
        "selected", "positive", "negative", "warning", "info", "focus-ring", "text-selection", "shadow", "overlay"
    ];

    static readonly (string Paths, string Properties)[] BaseRules =
    [
        ("button checkbox radio toggle input dropdown slider treeview breadcrumb label wrapped-label", "color"),
        ("button input dropdown chip listbox progress tabstrip tabbar tabpanel dock-panel dock-window",
            "background-color"),
        ("dock-grip dock-empty dock-ghost drop-indicator drop-preview menubar menu popup dialog toast tooltip appbar",
            "background-color"),
        ("popup-title dialog-title overlay scrollbar object-field file-sidebar file-list", "background-color"),
        ("chip listbox tabstrip>tab tabbar>tab tab-nav dock-empty dock-ghost menubar menu menu-shortcut menu-separator",
            "color"),
        ("tree-expander tree-ghost popup dialog toast tooltip popup-title dialog-title dialog-close appbar crumb",
            "color"),
        ("crumb-separator file-label file-status object-field object-pick object-clear file-row file-place file-column",
            "color"),
        ("file-detail input>placeholder color-channel>label", "color"),
        ("checkbox>indicator radio>indicator toggle>track toggle>thumb input>selection slider>track slider>fill",
            "background-color"),
        ("slider>thumb progress>fill tab>marker tree-ghost crumb>underline window-button>glyph-fill scrollbar>thumb",
            "background-color"),
        ("radio>dot:checked option:selected tree-row:selected toast.info>accent toast.positive>accent",
            "background-color"),
        ("toast.warning>accent toast.negative>accent", "background-color"),
        ("option>indicator color-area color-strip swatch color-preview file-list",
            "border-color"),
        ("file-header", "border-bottom-color"),
        ("dialog-footer", "border-top-color"),
        ("color-area>cursor color-strip>thumb", "width height border-color"),
        ("checker", "background-color color"),
        ("color-picker color-picker-row swatches color-hex color-channels color-channel color-field",
            "flex-direction"),
        ("slider>track progress", "height"),
        ("slider>thumb", "width height"),
        ("splitter:horizontal", "width cursor"),
        ("splitter:vertical", "height cursor"),
        ("toast>accent", "width"),
        ("form", "color dim-color background-color border-color accent-color divider-color negative-color " +
            "base-background border-radius")
    ];

    /// <summary>The GUI's selected base sheet, or <see cref="DefaultSheet"/> when no replacement is selected.</summary>
    /// <param name="gui">The GUI whose base sheet is requested.</param>
    public static StyleSheet GetBaseSheet(Gui gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        return Bases.TryGetValue(gui, out var sheet) ? sheet : DefaultSheet;
    }

    /// <summary>
    /// Selects a complete base sheet for this GUI; <c>null</c> restores <see cref="DefaultSheet"/>.
    /// The color overlay and the relative order of application sheets are preserved.
    /// </summary>
    /// <param name="gui">The GUI to theme.</param>
    /// <param name="sheet">The complete replacement, or <c>null</c> to reset the base.</param>
    /// <exception cref="ArgumentException">The replacement lacks required tokens or control declarations.</exception>
    public static void SetBaseSheet(Gui gui, StyleSheet? sheet)
    {
        var current = GetBaseSheet(gui);
        var selected = sheet ?? DefaultSheet;
        if (ReferenceEquals(current, selected))
        {
            Ensure(gui);
            return;
        }

        var errors = ValidateBaseSheet(selected);
        if (errors.Count != 0)
            throw new ArgumentException($"Incomplete Excalibur base sheet: {string.Join("; ", errors)}", nameof(sheet));

        gui.StyleSheets.Remove(current);
        if (Themes.TryGetValue(gui, out var theme) && ReferenceEquals(theme, selected))
        {
            gui.StyleSheets.Remove(theme);
            Themes.Remove(gui);
        }
        if (ReferenceEquals(selected, DefaultSheet)) Bases.Remove(gui);
        else Bases.AddOrUpdate(gui, selected);
        Ensure(gui);
    }

    /// <summary>
    /// Reports missing or invalid fallback tokens and missing essential control declarations in a standalone base.
    /// Validation resolves only this sheet, including its imports; application and color overlays cannot fill gaps.
    /// </summary>
    /// <param name="sheet">The complete base sheet to validate.</param>
    public static IReadOnlyList<string> ValidateBaseSheet(StyleSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var sheets = new StyleSheetCollection { sheet };
        var errors = new List<string>();
        ValidateBaseTokens(sheets, errors);
        foreach (var (paths, properties) in BaseRules)
            foreach (var path in paths.Split(' '))
                ValidateBaseRule(sheets, path, properties, errors);
        return errors;
    }

    static void ValidateBaseTokens(StyleSheetCollection sheets, List<string> errors)
    {
        foreach (var name in BaseColors)
            if (sheets.GetTokenColor(name) is null) errors.Add($"--{name}: expected a color");

        if (!StyleValue.TryLength(sheets.GetToken("radius"), out var radius, out var percent) || percent ||
            !float.IsFinite(radius) || radius < 0)
            errors.Add("--radius: expected a nonnegative pixel length");
    }

    static void ValidateBaseRule(StyleSheetCollection sheets, string path, string properties, List<string> errors)
    {
        var style = sheets.Resolve(BaseTarget(path));
        foreach (var property in properties.Split(' '))
        {
            var value = BaseProperty(style, path, property);
            if (string.IsNullOrWhiteSpace(value) || value.Contains('$') ||
                value.Contains("var(", StringComparison.Ordinal))
                errors.Add($"{path}: missing or unresolved {property}");
        }
    }

    static string? BaseProperty(ResolvedStyle style, string path, string property)
    {
        var value = style.Get(property);
        return property == "background-color" && path is not ("form" or "checker")
            ? value ?? style.Get("background")
            : value;
    }

    static StyleTarget BaseTarget(string path)
    {
        var parts = path.Split('>');
        var compound = parts[^1].Split(':');
        var names = compound[0].Split('.');
        return new StyleTarget(names[0], null, names[1..],
            Modifiers: compound[1..], Ancestors: parts.Length == 1 ? null : [BaseTarget(parts[0])]);
    }
}
