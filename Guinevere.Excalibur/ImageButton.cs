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
    /// <param name="content">Optional caption (any <see cref="Content"/>) centred over the background.</param>
    /// <param name="nineSlice">Corner sizes for nine-slice scaling, or <c>null</c> to stretch the whole image.</param>
    /// <param name="enabled">When false the button shows <paramref name="disabled"/> and never reports a click.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id for <c>button#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The button's node, sized to the normal image unless the sheet or the caller sizes it;
    /// <see cref="LayoutNode.Clicked"/> is true on the frame it is clicked or activated by keyboard.
    /// </returns>
    public static ControlNode ImageButton(
        this Gui gui,
        SKImage normal,
        SKImage hover,
        SKImage pressed,
        SKImage? disabled = null,
        Content content = default,
        Insets? nineSlice = null,
        bool enabled = true,
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
        if (node.Style.Width is null)
        {
            node.Width(normal.Width);
        }

        if (node.Style.Height is null)
        {
            node.Height(normal.Height);
        }

        node.Direction(Axis.Horizontal).AlignContent(0.5f);
        using (node.Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                // The image is the background: queue it under the sheet's border and focus ring.
                var image = enabled ? StateImage(gui, normal, hover, pressed) : disabled ?? normal;
                node.DrawList.Prepend(nineSlice is { } insets
                    ? new NineSliceDrawable(image, node.Rect, insets)
                    : new ImageDrawable(image, node.Rect));
                if (enabled && Activated(gui))
                {
                    node.Raise(NodeSignals.Clicked);
                }
            }

            gui.Content(content);
        }

        return node;
    }

    static readonly string[] ImageClass = ["image"];

    /// <summary>The image for the pointer's state; also registers the enabled button for keyboard focus.</summary>
    static SKImage StateImage(Gui gui, SKImage normal, SKImage hover, SKImage pressed)
    {
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var interactable = gui.GetInteractable();
        return interactable.OnHold() ? pressed : interactable.OnHover() ? hover : normal;
    }
}
