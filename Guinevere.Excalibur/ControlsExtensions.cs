using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] IconClass = ["icon"];

    /// <summary>
    /// A button styled by the <c>button</c> rules of the GUI's sheets (<c>:hover</c>, <c>:active</c>, <c>:focus</c>,
    /// <c>:disabled</c>). Without an explicit size it fits its text plus the rule's <c>padding</c>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="text">The caption.</param>
    /// <param name="width">Button width, or <c>0</c> to fit the text.</param>
    /// <param name="height">Button height, or <c>0</c> to fit the text.</param>
    /// <param name="fontSize">Caption size; defaults to the style's or the scope's text size.</param>
    /// <param name="enabled">When false the button matches <c>:disabled</c> and never reports a click.</param>
    /// <param name="classes">Extra classes for the sheet, such as <c>primary</c>.</param>
    /// <param name="id">Element id for <c>button#id</c> rules.</param>
    /// <returns><c>true</c> on the frame the button is clicked or activated by Space/Enter while focused.</returns>
    public static bool Button(this Gui gui, Text text,
        float width = 0, float height = 0,
        float? fontSize = null,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null) =>
        ButtonCore(gui, text, null, width, height, fontSize, enabled, classes, id);

    /// <summary>A square icon button (class <c>icon</c>) that stores whether it was clicked in <paramref name="clicked"/>.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="icon">The glyph, or <c>null</c> for an empty button.</param>
    /// <param name="clicked">Set to whether the button was clicked or activated this frame.</param>
    /// <param name="size">Side of the square.</param>
    /// <param name="fontSize">Glyph size; defaults to the style's or the scope's text size.</param>
    /// <param name="enabled">When false the button matches <c>:disabled</c> and never reports a click.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>button#id</c> rules.</param>
    public static void IconButton(this Gui gui, char? icon, ref bool clicked,
        float size = ControlMetrics.FieldHeight,
        float? fontSize = null,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null)
    {
        size = gui.ControlStyle.FieldHeightOr(size);
        clicked = ButtonCore(gui, icon?.ToString() ?? "", IconClass, size, size, fontSize, enabled, classes, id);
    }

    /// <summary>A square icon button (class <c>icon</c>) that returns whether it was clicked.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="icon">The glyph text.</param>
    /// <param name="size">Side of the square.</param>
    /// <param name="fontSize">Glyph size.</param>
    /// <param name="enabled">When false the button matches <c>:disabled</c> and never reports a click.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for <c>button#id</c> rules.</param>
    /// <returns><c>true</c> on the frame the button is clicked or activated by Space/Enter while focused.</returns>
    public static bool IconButton(this Gui gui, string icon,
        float size = ControlMetrics.FieldHeight,
        float fontSize = 16,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null)
    {
        size = gui.ControlStyle.FieldHeightOr(size);
        return ButtonCore(gui, icon, IconClass, size, size, fontSize, enabled, classes, id);
    }

    static bool ButtonCore(Gui gui, Text text, string[]? variant, float width, float height, float? fontSize,
        bool enabled, IReadOnlyList<string>? classes, string? id,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        var node = StyledButton(gui, variant, classes, id, enabled, filePath, lineNumber);
        var fontSizeEffective = fontSize ?? node.Scope.Get<LayoutNodeScopeTextSize>().Value;
        var (buttonWidth, buttonHeight) = CalculateButtonDimensions(gui, node, text, width, height, fontSizeEffective);

        using (node.Width(buttonWidth).Height(buttonHeight).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;
            if (enabled) gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            RenderCenteredText(gui, text, fontSizeEffective);
            return enabled && Activated(gui);
        }
    }

    /// <summary>The <c>button</c> node with its variant and caller classes, styled from the GUI's sheets.</summary>
    static LayoutNode StyledButton(Gui gui, string[]? variant, IReadOnlyList<string>? classes, string? id,
        bool enabled, string filePath, int lineNumber)
    {
        ExcaliburStyles.Ensure(gui);
        IReadOnlyList<string>? all = classes is null ? variant : variant is null ? classes : [.. variant, .. classes];
        return gui.StyledNode("button", all, id, disabled: !enabled, filePath: filePath, lineNumber: lineNumber);
    }

    /// <summary>Clicked this frame, or Space/Enter pressed while focused; a click also takes keyboard focus.</summary>
    static bool Activated(Gui gui)
    {
        var activated = gui.HasFocus()
                        && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter));
        var clicked = gui.GetInteractable().OnClick();
        if (clicked) gui.RequestFocus(FocusReason.Mouse);
        return clicked || activated;
    }

    static (float width, float height) CalculateButtonDimensions(Gui gui, LayoutNode node, Text text, float width,
        float height, float fontSize)
    {
        if (width > 0 && height > 0) return (width, height);

        // Measure through the same main/icon font fallback as Gui.DrawText so emoji and icon text
        // size the button to the glyphs that will actually render.
        var textWidth = 0f;
        var textHeight = 0f;
        foreach (var (runText, runFont) in TextRuns(gui, text.Label ?? "", fontSize))
        {
            runFont.SkFont.MeasureText(runText, out var bounds);
            textWidth += bounds.Width;
            textHeight = Math.Max(textHeight, bounds.Height);
        }

        var style = node.Style;
        return (
            width > 0 ? width : textWidth + style.PaddingLeft + style.PaddingRight,
            height > 0 ? height : textHeight + style.PaddingTop + style.PaddingBottom
        );
    }

    static (string Text, Font Font)[] TextRuns(Gui gui, string text, float fontSize)
    {
        var mainFont = gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value.Resized(fontSize);
        var iconFont = new Font(new SKFont(
            gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.SkFont.Typeface, fontSize));

        return [.. gui.CreateTextRuns(text, mainFont, iconFont).Select(run => (run.Text, run.Font))];
    }

    /// <summary>Draws a caption centred in the current node, in the scope's text color (which the style sets).</summary>
    static void RenderCenteredText(this Gui gui, Text? text, float fontSize)
    {
        var rect = gui.CurrentNode.Rect;
        var label = text?.Label ?? string.Empty;
        if (string.IsNullOrEmpty(label)) return;

        var paint = new SKPaint { Color = gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value, IsAntialias = true };

        // Draw through the same main/icon font fallback as Gui.DrawText so glyphs the text font
        // lacks -- emoji, icons -- render from the icon font instead of as tofu.
        var runs = TextRuns(gui, label, fontSize);

        var totalWidth = 0f;
        var ascent = 0f;
        var descent = 0f;
        foreach (var (runText, runFont) in runs)
        {
            runFont.SkFont.MeasureText(runText, out var bounds);
            totalWidth += bounds.Width;
            ascent = Math.Max(ascent, -bounds.Top);
            descent = Math.Max(descent, bounds.Bottom);
        }

        var cursorX = rect.X + (rect.W - totalWidth) * 0.5f;
        var baselineY = rect.Y + (rect.H - (ascent + descent)) * 0.5f + ascent;

        foreach (var (runText, runFont) in runs)
        {
            gui.CurrentNode.DrawList.Add(
                new Text(runText, new Vector2(cursorX, baselineY), runFont.SkFont, paint));
            runFont.SkFont.MeasureText(runText, out var bounds);
            cursorX += bounds.Width;
        }
    }
}
