using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// A <c>label</c> node fitting its content, which does not wrap. Size, font, color and text effects come from the
    /// sheet (<c>label.x</c> rules) or the parent scope; alignment and size go on the returned node. Use
    /// <see cref="WrappedLabel"/> for wrapped text.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="content">What to show: text, an icon, a localized string, other widgets or a custom type.</param>
    /// <param name="selectable">Whether text content supports pointer selection and copying.</param>
    /// <param name="classes">Stylesheet classes for the label.</param>
    /// <param name="id">Optional id for <c>label#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The label's node; a label reports no events, so it is a plain <see cref="LayoutNode"/>.</returns>
    public static LayoutNode Label(this Gui gui, Content content,
        bool selectable = false,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ExcaliburStyles.Ensure(gui);
        var node = gui.StyledNode("label", classes, id, filePath: filePath, lineNumber: lineNumber);
        FitUnlessStyled(node);
        node.Direction(Axis.Horizontal).AlignContentY(0.5f);
        using (node.Enter())
        {
            content.BuildIn(gui, selectable);
        }

        return node;
    }
}
