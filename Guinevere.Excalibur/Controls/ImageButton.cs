namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A button whose background is a bitmap that swaps on hover and press (and, when supplied, on
    /// disable). Optionally nine-sliced so one small texture scales to any size, and with an
    /// optional centred caption. Registers as focusable and activates on Space/Enter like
    /// <c>Button</c>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="normal">Background in the rest state.</param>
    /// <param name="hover">Background while hovered.</param>
    /// <param name="pressed">Background while held down.</param>
    /// <param name="disabled">Background while <paramref name="enabled"/> is false; falls back to <paramref name="normal"/>.</param>
    /// <param name="text">Optional caption drawn centred over the background.</param>
    /// <param name="width">Button width, or <c>0</c> to use the normal image width.</param>
    /// <param name="height">Button height, or <c>0</c> to use the normal image height.</param>
    /// <param name="nineSlice">Corner sizes for nine-slice scaling, or <c>null</c> to stretch the whole image.</param>
    /// <param name="enabled">When false the button shows <paramref name="disabled"/> and never reports a click.</param>
    /// <param name="textColor">Caption color; defaults to the scope text color.</param>
    /// <param name="fontSize">Caption size; defaults to the scope text size.</param>
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
        Color? textColor = null,
        float? fontSize = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(normal);
        ArgumentNullException.ThrowIfNull(hover);
        ArgumentNullException.ThrowIfNull(pressed);

        var node = gui.Node();
        var w = width > 0 ? width : normal.Width;
        var h = height > 0 ? height : normal.Height;

        using (node.Width(w).Height(h).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            var clicked = false;
            SKImage image;

            if (!enabled)
            {
                image = disabled ?? normal;
            }
            else
            {
                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                var interactable = gui.GetInteractable();
                image = interactable.OnHold() ? pressed : interactable.OnHover() ? hover : normal;

                if (gui.HasFocus())
                {
                    var rect = gui.CurrentNode.Rect;
                    gui.DrawRectBorder(
                        new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6),
                        Color.FromArgb(128, 100, 149, 237), 4f, 6f);
                }

                var activated = gui.HasFocus()
                                && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter));
                clicked = interactable.OnClick() || activated;
            }

            var bounds = gui.CurrentNode.Rect;
            if (nineSlice is { } insets)
                gui.DrawImageNineSlice(image, bounds, insets, null, enabled ? 1f : 0.6f, node);
            else
                gui.DrawImage(image, bounds, null, null, enabled ? 1f : 0.6f, node);

            if (text is { Label: { Length: > 0 } })
            {
                var size = fontSize ?? node.Scope.Get<LayoutNodeScopeTextSize>().Value;
                var color = textColor ?? node.Scope.Get<LayoutNodeScopeTextColor>().Value;
                if (!enabled) color = Color.FromArgb(color.A / 2, color.R, color.G, color.B);

                // Centres the caption by hand (like Button) with the same main/icon font fallback
                // as Gui.DrawText, so emoji and icon glyphs render instead of tofu. A child DrawText
                // node would need both passes, and this control only runs on the render pass.
                gui.RenderCenteredText(text, size, color);
            }

            return clicked;
        }
    }
}
