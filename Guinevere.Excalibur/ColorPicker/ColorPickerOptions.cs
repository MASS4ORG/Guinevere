using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>Builds a picker's content in place of the default layout; returns whether the color changed.</summary>
/// <param name="gui">The GUI context, inside the <c>color-picker</c> node.</param>
/// <param name="value">The edited color.</param>
/// <param name="original">The color before editing, when known, for a before/after preview.</param>
public delegate bool ColorPickerBody(Gui gui, ref Hsva value, Color? original);

/// <summary>
/// What the default color picker shows. Swap <see cref="Shape"/> and <see cref="Modes"/> to restyle it, or set
/// <see cref="Body"/> to replace its layout with parts of your own; <see cref="SetDefault"/> changes every picker and
/// color field of a GUI that is given no options.
/// </summary>
public sealed record ColorPickerOptions
{
    static readonly ConditionalWeakTable<Gui, ColorPickerOptions> Defaults = [];

    /// <summary>A small palette of saturated colors plus white, gray and black.</summary>
    public static IReadOnlyList<Color> DefaultSwatches { get; } =
    [
        Color.ParseHex("#ef4444"), Color.ParseHex("#f97316"), Color.ParseHex("#eab308"),
        Color.ParseHex("#22c55e"), Color.ParseHex("#14b8a6"), Color.ParseHex("#06b6d4"),
        Color.ParseHex("#3b82f6"), Color.ParseHex("#8b5cf6"), Color.ParseHex("#ec4899"),
        Color.ParseHex("#ffffff"), Color.ParseHex("#808080"), Color.ParseHex("#000000")
    ];

    /// <summary>The built-in look: HSV rectangle, hue and alpha strips, RGB/HSV/HSL fields, hex and swatches.</summary>
    public static ColorPickerOptions Default { get; } = new();

    /// <summary>The two-dimensional surface.</summary>
    public IColorPickerShape Shape { get; init; } = ColorPickerShapes.Rectangle;

    /// <summary>The channel models offered by the mode switch; the first is selected initially, none hides the fields.</summary>
    public IReadOnlyList<IColorPickerMode> Modes { get; init; } = ColorPickerModes.Default;

    /// <summary>Whether alpha is editable (strip, field and hex digits).</summary>
    public bool Alpha { get; init; } = true;

    /// <summary>Whether to show the hexadecimal field.</summary>
    public bool Hex { get; init; } = true;

    /// <summary>Whether the hexadecimal field has copy and paste buttons.</summary>
    public bool Clipboard { get; init; } = true;

    /// <summary>Preset swatches; empty hides the row.</summary>
    public IReadOnlyList<Color> Swatches { get; init; } = DefaultSwatches;

    /// <summary>Whether to show the GUI's <see cref="ColorRecents"/> below the presets.</summary>
    public bool ShowRecent { get; init; } = true;

    /// <summary>Picker width; 0 expands.</summary>
    public float Width { get; init; } = 240;

    /// <summary>Height of the surface and its strips.</summary>
    public float AreaHeight { get; init; } = 150;

    /// <summary>Height of a color field's popup; <c>null</c> estimates it from the default layout.</summary>
    public float? PopupHeight { get; init; }

    /// <summary>Replaces the default layout; the picker keeps its node, state and change reporting.</summary>
    public ColorPickerBody? Body { get; init; }

    /// <summary>The options used by a GUI's pickers and color fields when none are passed.</summary>
    /// <param name="gui">The GUI.</param>
    public static ColorPickerOptions For(Gui gui) => Defaults.TryGetValue(gui, out var options) ? options : Default;

    /// <summary>Sets the options a GUI's pickers and color fields use when none are passed; <c>null</c> restores the built-in look.</summary>
    /// <param name="gui">The GUI.</param>
    /// <param name="options">The new defaults.</param>
    public static void SetDefault(Gui gui, ColorPickerOptions? options)
    {
        ArgumentNullException.ThrowIfNull(gui);
        if (options is null)
        {
            Defaults.Remove(gui);
        }
        else
        {
            Defaults.AddOrUpdate(gui, options);
        }
    }
}

/// <summary>
/// Recently chosen colors, newest first, shared by a GUI's pickers. Color fields add their color when their popup
/// closes with a change; applications may add others.
/// </summary>
/// <param name="capacity">How many colors to keep.</param>
public sealed class ColorRecents(int capacity = 10)
{
    static readonly ConditionalWeakTable<Gui, ColorRecents> PerGui = [];
    readonly List<Color> _items = [];

    /// <summary>How many colors are kept.</summary>
    public int Capacity { get; } = Math.Max(1, capacity);

    /// <summary>The colors, newest first.</summary>
    public IReadOnlyList<Color> Items => _items;

    /// <summary>The list a GUI's pickers show.</summary>
    /// <param name="gui">The GUI.</param>
    public static ColorRecents For(Gui gui) => PerGui.GetValue(gui, _ => new ColorRecents());

    /// <summary>Moves or inserts a color at the front, dropping the oldest beyond <see cref="Capacity"/>.</summary>
    /// <param name="color">The color.</param>
    public void Add(Color color)
    {
        _items.Remove(color);
        _items.Insert(0, color);
        if (_items.Count > Capacity)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    /// <summary>Forgets every color.</summary>
    public void Clear() => _items.Clear();
}
