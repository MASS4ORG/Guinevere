using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] IconClass = ["icon"];

    /// <summary>
    /// A button styled by the <c>button</c> rules of the GUI's sheets (<c>:hover</c>, <c>:active</c>, <c>:focus</c>,
    /// <c>:disabled</c>), showing any <see cref="Content"/>: text, emoji, an icon, a localized string, other widgets or a
    /// custom type. It fits its content plus the rule's <c>padding</c>, centred; an icon-only button adds the <c>icon</c>
    /// class and is a square of the field height. Size, margin or placement go on the returned node.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="content">What the button shows; an icon should carry a label (<see cref="Icon.Labeled"/>).</param>
    /// <param name="enabled">When false the button matches <c>:disabled</c> and never reports a click.</param>
    /// <param name="classes">Extra classes for the sheet, such as <c>primary</c>.</param>
    /// <param name="id">Optional id for <c>button#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The button's node, <c>true</c> as a <c>bool</c> (and <see cref="LayoutNode.Clicked"/> after a chain) on the
    /// frame it is clicked or activated by Space/Enter while focused: <c>if (gui.Button("OK"))</c>.
    /// </returns>
    public static ControlNode Button(this Gui gui, Content content,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var iconOnly = content.Kind == ContentKind.Icon;
        var node = StyledButton(gui, iconOnly ? IconClass : null, classes, id, enabled, filePath, lineNumber);
        var side = gui.ControlStyle.FieldHeight;
        FitUnlessStyled(node, iconOnly ? side : null);
        node.Direction(Axis.Horizontal).AlignContent(0.5f);
        using (node.Enter())
        {
            ButtonInteraction(gui, node, content.Label, enabled);
            if (iconOnly)
            {
                // Relative, so an icon button sized on its node scales its icon with it.
                var icon = content.Icon!;
                gui.Icon(icon, side, icon.Color ?? node.Scope.Get<LayoutNodeScopeTextColor>().Value)
                    .Width(Size.Percentage(IconFraction)).Height(Size.Percentage(IconFraction));
            }
            else
            {
                gui.Content(content);
            }
        }

        return node;
    }

    /// <summary>An icon's side inside an icon button, as a fraction of the button.</summary>
    const float IconFraction = 2f / 3f;

    /// <summary>
    /// Sizes the node to its content, or to a <paramref name="square"/> side, on each axis the sheet left unsized.
    /// </summary>
    static void FitUnlessStyled(LayoutNode node, float? square = null)
    {
        var size = square is { } side ? Size.Pixels(side) : Size.FitContent();
        SizeUnlessStyled(node, size, size);
    }

    /// <summary>Sets <paramref name="width"/> and <paramref name="height"/> on each axis the sheet left unsized; null leaves the axis alone.</summary>
    static void SizeUnlessStyled(LayoutNode node, Size? width, Size? height)
    {
        if (width is { } w && node.Style.Width is null)
        {
            node.Width(w);
        }

        if (height is { } h && node.Style.Height is null)
        {
            node.Height(h);
        }
    }

    /// <summary>Pads the node when the sheet set no padding on any side.</summary>
    static void PadUnlessStyled(LayoutNode node, float horizontal, float vertical)
    {
        var style = node.Style;
        if (style.PaddingLeft.IsZero && style.PaddingRight.IsZero && style.PaddingTop.IsZero
            && style.PaddingBottom.IsZero)
        {
            node.Padding(horizontal, vertical);
        }
    }

    /// <summary>Gives the node's content the scope's control font size unless the sheet set one on it.</summary>
    static void UseControlFontSize(Gui gui, LayoutNode node) => UseFontSize(gui, node, gui.ControlStyle.FontSize);

    /// <summary>
    /// Makes <paramref name="size"/> the control font size of the current node's scope, so the fields and dropdowns
    /// built inside it use it; a composite widget calls it where it used to pass a font size to each control.
    /// </summary>
    static void UseControlFontSizeInScope(Gui gui, float size) =>
        gui.CurrentNodeScope.Set(ControlStyles.Value<ControlFontSize, float>(size));

    /// <summary>Gives the node's content <paramref name="size"/> unless the sheet set a font size on it.</summary>
    static void UseFontSize(Gui gui, LayoutNode node, float size)
    {
        if (!node.Scope.HasLocal<LayoutNodeScopeTextSize>())
        {
            gui.SetTextSize(size, node.Scope);
        }
    }

    /// <summary>
    /// In the render pass, registers the current button for focus, publishes its semantics and raises
    /// <see cref="NodeSignals.Clicked"/> on activation.
    /// </summary>
    static void ButtonInteraction(Gui gui, LayoutNode node, string? label, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        if (enabled)
        {
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        }

        PublishMenuSemantics(gui, label ?? "", enabled, false, ControlRole.Button);
        if (enabled && Activated(gui))
        {
            node.Raise(NodeSignals.Clicked);
        }
    }

    /// <summary>The <c>button</c> node with its variant and caller classes, styled from the GUI's sheets.</summary>
    static ControlNode StyledButton(Gui gui, string[]? variant, IReadOnlyList<string>? classes, string? id,
        bool enabled, string filePath, int lineNumber)
    {
        ExcaliburStyles.Ensure(gui);
        var all = classes is null ? variant : variant is null ? classes : [.. variant, .. classes];
        return gui.StyledControl("button", all, id, disabled: !enabled, filePath: filePath, lineNumber: lineNumber);
    }

    /// <summary>Clicked this frame, or Space/Enter pressed while focused; a click also takes keyboard focus.</summary>
    static bool Activated(Gui gui)
    {
        var activated = gui.HasFocus() && (gui.IsAcceptPressed
            || gui.ControlActivation?.IsActivated(gui.CurrentNode.Id) == true);
        var clicked = gui.GetInteractable().OnClick();
        if (clicked)
        {
            gui.RequestFocus(FocusReason.Mouse);
        }

        return clicked || activated;
    }
}
