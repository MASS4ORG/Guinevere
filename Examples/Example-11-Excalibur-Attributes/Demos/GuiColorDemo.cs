using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [GUIColor] tints a member's controls; nested content inherits the tint.
public sealed class GuiColorDemo
{
    [GuiColor("#E0605A")] public int Health { get; set; } = 100;

    [GuiColor(0.35f, 0.75f, 1f)] public int Mana { get; set; } = 50;

    [GuiColor("#7CCB6B")] public AudioSettings TintedNested { get; set; } = new();
}
