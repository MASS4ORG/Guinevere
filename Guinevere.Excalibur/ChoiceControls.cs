namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] CheckedModifier = [StyleModifiers.Checked];
    static readonly string[] MixedModifier = [StyleModifiers.Mixed];
    static readonly string[] NoModifiers = [];

    /// <summary>
    /// The row of a checkbox, radio or toggle: a styled <paramref name="type"/> node sized to its indicator plus the
    /// label measured in the scope font, laid out horizontally with <paramref name="spacing"/>.
    /// </summary>
    static LayoutNode ChoiceRowNode(Gui gui, string type, string label, float indicatorWidth, float indicatorHeight,
        float fontSize, float spacing, bool enabled, IReadOnlyList<string>? classes, string? id,
        string filePath, int lineNumber)
    {
        ExcaliburStyles.Ensure(gui);
        var node = gui.StyledNode(type, classes, id, NoModifiers, disabled: !enabled, filePath: filePath,
            lineNumber: lineNumber);
        var width = string.IsNullOrEmpty(label)
            ? indicatorWidth
            : indicatorWidth + spacing + gui.MeasureTextWidth(label, fontSize, node.Scope);
        return node
            .Width(width).Height(Math.Max(indicatorHeight, fontSize * gui.FontScale + 4))
            .Direction(Axis.Horizontal).Gap(spacing);
    }

    /// <summary>
    /// Registers the current row as focusable and reports a click (which also takes focus) or Space while focused.
    /// Render pass only; a disabled row never activates.
    /// </summary>
    static bool ChoiceActivated(Gui gui, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render || !enabled) return false;
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var clicked = gui.GetInteractable().OnClick();
        if (clicked) gui.RequestFocus(FocusReason.Mouse);
        return clicked || (gui.HasFocus() && gui.Input.IsKeyPressed(KeyboardKey.Space));
    }

    /// <summary>A styled part of a choice control, such as its <c>indicator</c>, matched with its modifiers.</summary>
    static LayoutNode ChoicePart(Gui gui, string type, float width, float height, IReadOnlyList<string> modifiers) =>
        gui.StyledNode(type, modifiers: modifiers).Width(width).Height(height);

    /// <summary>The label, in the row's text color.</summary>
    static void ChoiceLabel(Gui gui, string label, float fontSize)
    {
        if (!string.IsNullOrEmpty(label)) gui.DrawText(label, fontSize, centerInRect: false);
    }
}
