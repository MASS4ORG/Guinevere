namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Draws a selectable label that does not wrap by default. Use <see cref="WrappedLabel"/> for wrapped text.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="color">The text color. Defaults to the control style text.</param>
    /// <param name="selectable">Whether the label supports pointer selection and copying.</param>
    /// <param name="centerInRect">Whether to center the text in its node rect.</param>
    /// <param name="font">The font to draw with, or null for the default.</param>
    /// <param name="effects">Text effects to apply.</param>
    public static void Label(this Gui gui, string text,
        float fontSize = ControlMetrics.FontSize,
        Color? color = null,
        bool selectable = false,
        bool centerInRect = false,
        Font? font = null,
        TextEffects? effects = null)
    {
        gui.DrawText(text, selectable, fontSize, color, font, 0, centerInRect, clip: false, effects, layout: null);
    }
}
