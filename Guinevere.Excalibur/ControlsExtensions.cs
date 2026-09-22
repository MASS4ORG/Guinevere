namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Creates a button that returns the clicked state without modifying the input
    /// </summary>
    public static bool Button(this Gui gui, Text text,
        float width = 0, float height = 0,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? hoverColor = null,
        Color? pressedColor = null,
        Color? pressedBorderColor = null,
        Color? color = null,
        float? fontSize = null,
        float radius = ControlMetrics.CornerRadius,
        bool enabled = true)
    {
        radius = gui.ControlStyle.CornerRadiusOr(radius);

        return ButtonCore(gui, text, width, height, backgroundColor, borderColor, hoverColor,
            pressedColor, pressedBorderColor, color, fontSize, radius, enabled);
    }

    /// <summary>
    /// Creates an icon button that can be clicked with internal state management
    /// </summary>
    public static void IconButton(this Gui gui, char? icon, ref bool clicked,
        float size = ControlMetrics.FieldHeight,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? hoverColor = null,
        Color? pressedColor = null,
        Color? pressedBorderColor = null,
        Color? color = null,
        float? fontSize = null,
        float radius = ControlMetrics.CornerRadius,
        bool enabled = true)
    {
        size = gui.ControlStyle.FieldHeightOr(size);
        radius = gui.ControlStyle.CornerRadiusOr(radius);

        clicked = IconButtonCore(gui, icon, size, backgroundColor, borderColor, hoverColor,
            pressedColor, pressedBorderColor, color, fontSize, radius, enabled);
    }

    /// <summary>
    /// Creates an icon button that returns the clicked state without modifying the input
    /// </summary>
    public static bool IconButton(this Gui gui, string icon,
        float size = ControlMetrics.FieldHeight,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? hoverColor = null,
        Color? pressedColor = null,
        Color? pressedBorderColor = null,
        Color? color = null,
        float fontSize = 16,
        float radius = ControlMetrics.CornerRadius,
        bool enabled = true)
    {
        size = gui.ControlStyle.FieldHeightOr(size);
        radius = gui.ControlStyle.CornerRadiusOr(radius);

        return IconButtonCore(gui, icon, size, backgroundColor, borderColor, hoverColor,
            pressedColor, pressedBorderColor, color, fontSize, radius, enabled);
    }

    static bool ButtonCore(Gui gui, Text text, float width, float height,
        Color? backgroundColor, Color? borderColor,
        Color? hoverColor,
        Color? pressedColor, Color? pressedBorderColor,
        Color? color,
        float? fontSize, float radius, bool enabled)
    {
        var node = gui.Node();
        var fontSizeEffective = fontSize ?? node.Scope.Get<LayoutNodeScopeTextSize>().Value;
        var (buttonWidth, buttonHeight) = CalculateButtonDimensions(gui, text, width, height, fontSizeEffective);

        using (node.Width(buttonWidth).Height(buttonHeight).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            if (!enabled)
            {
                gui.DrawBackgroundRect(gui.ControlStyle.Surface, radius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, gui.ControlStyle.Border, 1f, radius);
                RenderCenteredText(gui, text, fontSizeEffective, gui.ControlStyle.TextDisabled);
                return false;
            }

            // Register as focusable for keyboard navigation
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            var interactable = gui.GetInteractable();
            RenderButtonBackground(gui, interactable, backgroundColor, hoverColor, pressedColor, radius);
            RenderButtonBorder(gui, interactable, borderColor, pressedBorderColor);
            RenderCenteredText(gui, text, fontSizeEffective, color);

            // Draw strong focus indicator if focused
            if (gui.HasFocus())
            {
                var rect = gui.CurrentNode.Rect;
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, gui.ControlStyle.AccentSubtle, 4f, radius + 4); // subtle glow
                gui.DrawRectBorder(rect, gui.ControlStyle.AccentHover, 2f, radius + 2); // strong blue border
            }

            // Keyboard activation (Space/Enter)
            var activated = gui.HasFocus()
                            && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter));

            var clicked = interactable.OnClick();
            if (clicked) gui.RequestFocus(FocusReason.Mouse);
            return clicked || activated;
        }
    }

    static bool IconButtonCore(Gui gui, Text? icon, float size,
        Color? backgroundColor, Color? borderColor,
        Color? hoverColor,
        Color? pressedColor, Color? pressedBorderColor,
        Color? color,
        float? fontSize, float radius, bool enabled)
    {
        var node = gui.Node();
        var fontSizeEffective = fontSize ?? node.Scope.Get<LayoutNodeScopeTextSize>().Value;
        var iconText = icon ?? new Text("");
        var (buttonWidth, buttonHeight) = CalculateButtonDimensions(gui, iconText, size, size, fontSizeEffective);

        using (gui.Node(buttonWidth, buttonHeight).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            if (!enabled)
            {
                gui.DrawBackgroundRect(gui.ControlStyle.Surface, radius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, gui.ControlStyle.Border, 1f, radius);
                RenderCenteredText(gui, icon, fontSizeEffective, gui.ControlStyle.TextDisabled);
                return false;
            }

            // Register as focusable for keyboard navigation
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            var interactable = gui.GetInteractable();
            RenderIconButtonBackground(gui, interactable, backgroundColor, hoverColor, pressedColor, radius);
            RenderButtonBorder(gui, interactable, borderColor, pressedBorderColor);
            RenderCenteredText(gui, icon, fontSizeEffective, color);

            // Draw strong focus indicator if focused
            if (gui.HasFocus())
            {
                var rect = gui.CurrentNode.Rect;
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, gui.ControlStyle.AccentSubtle, 4f, radius + 4); // subtle glow
                gui.DrawRectBorder(rect, gui.ControlStyle.AccentHover, 2f, radius + 2); // strong blue border
            }

            // Keyboard activation (Space/Enter)
            var activated = gui.HasFocus()
                            && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter));

            var clicked = interactable.OnClick();
            if (clicked) gui.RequestFocus(FocusReason.Mouse);
            return clicked || activated;
        }
    }

    static (float width, float height) CalculateButtonDimensions(Gui gui, Text text, float width, float height,
        float fontSize)
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

        const float paddingH = 16f;
        const float paddingV = 8f;

        return (
            width > 0 ? width : textWidth + paddingH * 2,
            height > 0 ? height : textHeight + paddingV * 2
        );
    }

    static (string Text, Font Font)[] TextRuns(Gui gui, string text, float fontSize)
    {
        var mainFont = new Font(new SKFont(
            gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value.SkFont.Typeface, fontSize));
        var iconFont = new Font(new SKFont(
            gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.SkFont.Typeface, fontSize));

        return [.. gui.CreateTextRuns(text, mainFont, iconFont).Select(run => (run.Text, run.Font))];
    }

    // private static (bool hovered, bool pressed, bool clicked) GetButtonInteractionState(Gui gui)
    // {
    //     var interactable = gui.GetInteractable();
    //     return (interactable.OnHover(), interactable.OnHold(), interactable.OnClick());
    // }

    static void RenderButtonBackground(Gui gui, InteractableElement interactable,
        Color? backgroundColor, Color? hoverColor, Color? pressedColor, float radius)
    {
        var buttonColor = GetButtonBackgroundColor(gui, interactable, backgroundColor, hoverColor, pressedColor);
        gui.DrawBackgroundRect(buttonColor, radius);
    }

    static void RenderIconButtonBackground(Gui gui, InteractableElement interactable,
        Color? backgroundColor, Color? hoverColor, Color? pressedColor, float radius)
    {
        if (!ShouldDrawIconButtonBackground(interactable, backgroundColor)) return;

        var buttonColor = GetButtonBackgroundColor(gui, interactable, backgroundColor, hoverColor, pressedColor);
        gui.DrawBackgroundRect(buttonColor, radius);
    }

    static void RenderButtonBorder(Gui gui, InteractableElement interactable,
        Color? borderColor, Color? pressedBorderColor)
    {
        if (!interactable.On(Interactions.Hover | Interactions.Click))
            return;

        var rect = gui.CurrentNode.Rect;
        var borderColorFinal = GetButtonBorderColor(gui, interactable, borderColor, pressedBorderColor);
        gui.DrawRectBorder(rect.Position, rect.Size, borderColorFinal, 1f, 4f);
    }

    static void RenderCenteredText(this Gui gui, Text? text, float fontSize, Color? color)
    {
        var rect = gui.CurrentNode.Rect;
        var textColorFinal = color ?? gui.ControlStyle.Text;
        var label = text?.Label ?? string.Empty;
        if (string.IsNullOrEmpty(label)) return;

        var paint = new SKPaint { Color = textColorFinal, IsAntialias = true };

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

    // Color calculation helpers
    static Color GetButtonBackgroundColor(Gui gui, InteractableElement interactable,
        Color? backgroundColor, Color? hoverColor, Color? pressedColor)
    {
        if (interactable.OnClick() && pressedColor.HasValue)
            return pressedColor.Value;
        if (interactable.OnHover() && hoverColor.HasValue)
            return hoverColor.Value;
        return backgroundColor ?? gui.ControlStyle.Surface;
    }

    static Color GetButtonBorderColor(Gui gui, InteractableElement interactable,
        Color? borderColor, Color? pressedBorderColor)
    {
        if (interactable.OnClick())
            return pressedBorderColor ?? gui.ControlStyle.BorderActive;
        return borderColor ?? gui.ControlStyle.Border;
    }

    static bool ShouldDrawIconButtonBackground(InteractableElement interactable,
        Color? backgroundColor)
    {
        return backgroundColor.HasValue || interactable.On(Interactions.Hover | Interactions.Click);
    }
}
