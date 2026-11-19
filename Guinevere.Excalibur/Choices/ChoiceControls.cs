namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] CheckedModifier = [StyleModifiers.Checked];
    static readonly string[] MixedModifier = [StyleModifiers.Mixed];
    static readonly string[] NoModifiers = [];

    /// <summary>
    /// The row of a checkbox, radio or toggle: a styled <paramref name="type"/> control laid out horizontally, fitting
    /// its indicator and label with the scope's control spacing between them. The label takes the control font size
    /// unless the sheet sets one.
    /// </summary>
    static ControlNode ChoiceRow(Gui gui, string type, bool enabled, IReadOnlyList<string>? classes, string? id,
        string filePath, int lineNumber)
    {
        ExcaliburStyles.Ensure(gui);
        var spacing = gui.ControlStyle.Spacing;
        var row = gui.StyledControl(type, classes, id, NoModifiers, disabled: !enabled, filePath: filePath,
            lineNumber: lineNumber);
        UseControlFontSize(gui, row);
        FitUnlessStyled(row);
        row.Direction(Axis.Horizontal).Gap(spacing).AlignContentY(0.5f);
        return row;
    }

    /// <summary>
    /// Registers the current row as focusable and reports a click (which also takes focus) or Space while focused,
    /// raising <see cref="NodeSignals.Clicked"/> on <paramref name="row"/>. Render pass only; a disabled row never
    /// activates.
    /// </summary>
    static bool ChoiceActivated(Gui gui, LayoutNode row, bool enabled)
    {
        if (!ChoiceActivated(gui, enabled))
        {
            return false;
        }

        row.Raise(NodeSignals.Clicked);
        return true;
    }

    /// <summary>
    /// Registers the current row as focusable and reports a click (which also takes focus) or Space while focused.
    /// Render pass only; a disabled row never activates.
    /// </summary>
    static bool ChoiceActivated(Gui gui, bool enabled)
    {
        if (gui.Pass != Pass.Pass2Render || !enabled)
        {
            return false;
        }

        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var clicked = gui.GetInteractable().OnClick();
        if (clicked)
        {
            gui.RequestFocus(FocusReason.Mouse);
        }

        return clicked || (gui.HasFocus() && gui.Input.IsKeyPressed(KeyboardKey.Space));
    }

    /// <summary>
    /// A styled part of a choice control, such as its <c>indicator</c>, matched with its modifiers and sized
    /// <paramref name="width"/> by <paramref name="height"/> on each axis the sheet leaves unsized.
    /// </summary>
    static LayoutNode ChoicePart(Gui gui, string type, Size width, Size height, IReadOnlyList<string> modifiers)
    {
        var part = gui.StyledNode(type, modifiers: modifiers);
        SizeUnlessStyled(part, width, height);
        return part;
    }
}
