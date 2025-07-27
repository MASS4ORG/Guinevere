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
        float radius = 4)
    {
        return ButtonCore(gui, text, width, height, backgroundColor, borderColor, hoverColor,
            pressedColor, pressedBorderColor, color, fontSize, radius);
    }

    /// <summary>
    /// Creates an icon button that can be clicked with internal state management
    /// </summary>
    public static void IconButton(this Gui gui, char? icon, ref bool clicked,
        float size = 32,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? hoverColor = null,
        Color? pressedColor = null,
        Color? pressedBorderColor = null,
        Color? color = null,
        float? fontSize = null,
        float radius = 4)
    {
        clicked = IconButtonCore(gui, icon, size, backgroundColor, borderColor, hoverColor,
            pressedColor, pressedBorderColor, color, fontSize, radius);
    }

    /// <summary>
    /// Creates an icon button that returns the clicked state without modifying the input
    /// </summary>
    public static bool IconButton(this Gui gui, string icon,
        float size = 32,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? hoverColor = null,
        Color? pressedColor = null,
        Color? pressedBorderColor = null,
        Color? color = null,
        float fontSize = 16,
        float radius = 4)
    {
        return IconButtonCore(gui, icon, size, backgroundColor, borderColor, hoverColor,
            pressedColor, pressedBorderColor, color, fontSize, radius);
    }

    private static bool ButtonCore(Gui gui, Text text, float width, float height,
        Color? backgroundColor, Color? borderColor,
        Color? hoverColor,
        Color? pressedColor, Color? pressedBorderColor,
        Color? color,
        float? fontSize, float radius)
    {
        var node = gui.Node();
        var fontSizeEffective = fontSize ?? node.Scope.Get<LayoutNodeScopeTextSize>().Value;
        var (buttonWidth, buttonHeight) = CalculateButtonDimensions(text, width, height, fontSizeEffective);

        using (node.Width(buttonWidth).Height(buttonHeight).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            // Register as focusable for keyboard navigation
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            var interactable = gui.GetInteractable();
            RenderButtonBackground(gui, interactable, backgroundColor, hoverColor, pressedColor, radius);
            RenderButtonBorder(gui, interactable, borderColor, pressedBorderColor);
            RenderButtonText(gui, text, fontSizeEffective, color);

            // Draw strong focus indicator if focused
            if (gui.HasFocus())
            {
                var rect = gui.CurrentNode.Rect;
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, Color.FromArgb(128, 100, 149, 237), 4f, radius + 4); // subtle glow
                gui.DrawRectBorder(rect, Color.FromArgb(255, 100, 149, 237), 2f, radius + 2); // strong blue border
            }

            // Keyboard activation (Space/Enter)
            var activated = false;
            if (gui.HasFocus() && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter)))
            {
                activated = true;
            }

            return interactable.OnClick() || activated;
        }
    }

    private static bool IconButtonCore(Gui gui, Text? icon, float size,
        Color? backgroundColor, Color? borderColor,
        Color? hoverColor,
        Color? pressedColor, Color? pressedBorderColor,
        Color? color,
        float? fontSize, float radius)
    {
        var node = gui.Node();
        var fontSizeEffective = fontSize ?? node.Scope.Get<LayoutNodeScopeTextSize>().Value;
        var iconText = icon ?? new Text("");
        var (buttonWidth, buttonHeight) = CalculateButtonDimensions(iconText, size, size, fontSizeEffective);

        using (gui.Node(buttonWidth, buttonHeight).Enter())
        {
            if (gui.Pass != Pass.Pass2Render) return false;

            // Register as focusable for keyboard navigation
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            var interactable = gui.GetInteractable();
            RenderIconButtonBackground(gui, interactable, backgroundColor, hoverColor, pressedColor, radius);
            RenderButtonBorder(gui, interactable, borderColor, pressedBorderColor);
            RenderButtonIcon(gui, icon, fontSizeEffective, color);

            // Draw strong focus indicator if focused
            if (gui.HasFocus())
            {
                var rect = gui.CurrentNode.Rect;
                var focusRect = new Rect(rect.X - 3, rect.Y - 3, rect.W + 6, rect.H + 6);
                gui.DrawRectBorder(focusRect, Color.FromArgb(128, 100, 149, 237), 4f, radius + 4); // subtle glow
                gui.DrawRectBorder(rect, Color.FromArgb(255, 100, 149, 237), 2f, radius + 2); // strong blue border
            }

            // Keyboard activation (Space/Enter)
            var activated = false;
            if (gui.HasFocus() && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter)))
            {
                activated = true;
            }

            return interactable.OnClick() || activated;
        }
    }

    private static (float width, float height) CalculateButtonDimensions(Text text, float width, float height,
        float fontSize)
    {
        if (width > 0 && height > 0) return (width, height);

        var font = new SKFont { Size = fontSize };
        font.MeasureText(text.Label ?? "", out var textBounds);

        const float paddingH = 16f;
        const float paddingV = 8f;

        return (
            width > 0 ? width : textBounds.Width + paddingH * 2,
            height > 0 ? height : textBounds.Height + paddingV * 2
        );
    }

    // private static (bool hovered, bool pressed, bool clicked) GetButtonInteractionState(Gui gui)
    // {
    //     var interactable = gui.GetInteractable();
    //     return (interactable.OnHover(), interactable.OnHold(), interactable.OnClick());
    // }

    private static void RenderButtonBackground(Gui gui, InteractableElement interactable,
        Color? backgroundColor, Color? hoverColor, Color? pressedColor, float radius)
    {
        var buttonColor = GetButtonBackgroundColor(interactable, backgroundColor, hoverColor, pressedColor);
        gui.DrawBackgroundRect(buttonColor, radius);
    }

    private static void RenderIconButtonBackground(Gui gui, InteractableElement interactable,
        Color? backgroundColor, Color? hoverColor, Color? pressedColor, float radius)
    {
        if (!ShouldDrawIconButtonBackground(interactable, backgroundColor)) return;

        var buttonColor = GetButtonBackgroundColor(interactable, backgroundColor, hoverColor, pressedColor);
        gui.DrawBackgroundRect(buttonColor, radius);
    }

    private static void RenderButtonBorder(Gui gui, InteractableElement interactable,
        Color? borderColor, Color? pressedBorderColor)
    {
        if (!interactable.On(Interactions.Hover | Interactions.Click))
            return;

        var rect = gui.CurrentNode.Rect;
        var borderColorFinal = GetButtonBorderColor(interactable, borderColor, pressedBorderColor);
        gui.DrawRectBorder(rect.Position, rect.Size, borderColorFinal, 1f, 4f);
    }

    // private static void RenderIconButtonBorder(Gui gui, InteractableElement interactable,
    //     Color? borderColor)
    // {
    //     if (!interactable.On(Interactions.Hover | Interactions.Click))
    //         return;
    //
    //     var rect = gui.CurrentNode.Rect;
    //     gui.DrawRectBorder(rect.Position, rect.Size, borderColor.Value, 1f, 4f);
    // }

    private static void RenderButtonText(Gui gui, Text? text, float fontSize, Color? color)
    {
        var rect = gui.CurrentNode.Rect;
        var textColorFinal = color ?? Color.White;
        var font = new SKFont { Size = fontSize };
        var paint = new SKPaint { Color = textColorFinal, IsAntialias = true };

        font.MeasureText(text?.Label ?? string.Empty, out var textBounds);
        var textPos = CalculateTextCenterPosition(rect, textBounds);

        var textShape = new Text(text?.Label ?? string.Empty, textPos, font, paint);
        gui.CurrentNode.DrawList.Add(textShape);
    }

    private static void RenderButtonIcon(Gui gui, Text? icon, float fontSize, Color? iconColor)
    {
        var rect = gui.CurrentNode.Rect;
        var iconColorFinal = iconColor ?? Color.White;
        var font = new SKFont { Size = fontSize };
        var paint = new SKPaint { Color = iconColorFinal, IsAntialias = true };

        font.MeasureText(icon?.Label ?? string.Empty, out var iconBounds);
        var iconPos = CalculateTextCenterPosition(rect, iconBounds);

        var iconShape = new Text(icon?.Label ?? string.Empty, iconPos, font, paint);
        gui.CurrentNode.DrawList.Add(iconShape);
    }

    // Color calculation helpers
    private static Color GetButtonBackgroundColor(InteractableElement interactable,
        Color? backgroundColor, Color? hoverColor, Color? pressedColor)
    {
        if (interactable.OnClick() && pressedColor.HasValue)
            return pressedColor.Value;
        if (interactable.OnHover() && hoverColor.HasValue)
            return hoverColor.Value;
        return backgroundColor ?? Color.FromArgb(255, 100, 149, 237);
    }

    private static Color GetButtonBorderColor(InteractableElement interactable,
        Color? borderColor, Color? pressedBorderColor)
    {
        if (interactable.OnClick())
            return pressedBorderColor ?? Color.FromArgb(255, 60, 109, 197);
        return borderColor ?? Color.FromArgb(255, 120, 169, 255);
    }

    private static bool ShouldDrawIconButtonBackground(InteractableElement interactable,
        Color? backgroundColor)
    {
        return backgroundColor.HasValue || interactable.On(Interactions.Hover | Interactions.Click);
    }

    private static Vector2 CalculateTextCenterPosition(Rect rect, SKRect textBounds)
    {
        return new Vector2(
            rect.X + (rect.W - textBounds.Width) * 0.5f,
            rect.Y + (rect.H + textBounds.Height) * 0.5f
        );
    }
}
