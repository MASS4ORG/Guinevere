using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A button whose background is a bitmap that swaps on hover and press (and, when supplied, on
    /// disable). Optionally nine-sliced so one small texture scales to any size, and with an
    /// optional centered caption. Registers as focusable and activates on Space/Enter like
    /// <c>Button</c>. The focus ring, caption color and disabled fade come from <c>button.image</c> rules.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="normal">Background in the rest state.</param>
    /// <param name="hover">Background while hovered.</param>
    /// <param name="pressed">Background while held down.</param>
    /// <param name="disabled">Background while <paramref name="enabled"/> is false; falls back to <paramref name="normal"/>.</param>
    /// <param name="text">Optional caption drawn centered over the background.</param>
    /// <param name="width">Button width, or <c>0</c> to use the normal image width.</param>
    /// <param name="height">Button height, or <c>0</c> to use the normal image height.</param>
    /// <param name="nineSlice">Corner sizes for nine-slice scaling, or <c>null</c> to stretch the whole image.</param>
    /// <param name="enabled">When false the button shows <paramref name="disabled"/> and never reports a click.</param>
    /// <param name="fontSize">Caption size; defaults to the style's or the scope's text size.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>button#id</c> rules.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns><c>true</c> on the frame the button is clicked or activated by keyboard.</returns>
    public static bool ImageButton(
        this Gui gui,
        SKImage normal,
        SKImage hover,
        SKImage pressed,
        SKImage? disabled = null,
        Text? text = null,
        float width = 0,
        float height = 0,
        Insets? nineSlice = null,
        bool enabled = true,
        float? fontSize = null,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(normal);
        ArgumentNullException.ThrowIfNull(hover);
        ArgumentNullException.ThrowIfNull(pressed);

        var node = StyledButton(gui, ImageClass, classes, id, enabled, filePath, lineNumber);
        using (node.Width(width > 0 ? width : normal.Width).Height(height > 0 ? height : normal.Height).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            // The image is the background: queue it under the sheet's border and focus ring.
            var image = enabled ? StateImage(gui, normal, hover, pressed) : disabled ?? normal;
            node.DrawList.Prepend(nineSlice is { } insets
                ? new NineSliceDrawable(image, node.Rect, insets)
                : new ImageDrawable(image, node.Rect));
            DrawCaption(gui, node, text, fontSize);
            return enabled && Activated(gui);
        }
    }

    static readonly string[] ImageClass = ["image"];

    /// <summary>The image for the pointer's state; also registers the enabled button for keyboard focus.</summary>
    static SKImage StateImage(Gui gui, SKImage normal, SKImage hover, SKImage pressed)
    {
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var interactable = gui.GetInteractable();
        return interactable.OnHold() ? pressed : interactable.OnHover() ? hover : normal;
    }

    /// <summary>
    /// Centres the caption by hand (like Button) with the same main/icon font fallback as Gui.DrawText, so emoji and
    /// icon glyphs render instead of tofu; this control only draws in the render pass.
    /// </summary>
    static void DrawCaption(Gui gui, LayoutNode node, Text? text, float? fontSize)
    {
        if (string.IsNullOrEmpty(text?.Label)) return;
        gui.RenderCenteredText(text, fontSize ?? node.Scope.Get<LayoutNodeScopeTextSize>().Value);
    }
}
