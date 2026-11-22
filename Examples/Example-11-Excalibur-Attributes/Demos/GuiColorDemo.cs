using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [GUIColor] tints a member's controls; nested content inherits the tint.
/// <summary>Demonstrates applying colors to generated controls.</summary>
public sealed class GuiColorDemo
{
    /// <summary>Health value with a hexadecimal control tint.</summary>
    [GuiColor("#E0605A")] public int Health { get; set; } = 100;

    /// <summary>Mana value with an RGB control tint.</summary>
    [GuiColor(0.35f, 0.75f, 1f)] public int Mana { get; set; } = 50;

    /// <summary>Nested audio settings that inherit the configured tint.</summary>
    [GuiColor("#7CCB6B")] public AudioSettings TintedNested { get; set; } = new();
}
