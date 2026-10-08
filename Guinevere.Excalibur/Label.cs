namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Draws a selectable label that does not wrap by default. Use <see cref="WrappedLabel"/> for wrapped text.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="classes">Stylesheet classes for the label.</param>
    /// <param name="id">Stable stylesheet identity.</param>
    /// <param name="selectable">Whether the label supports pointer selection and copying.</param>
    /// <param name="centerInRect">Whether to center the text in its node rect.</param>
    /// <param name="font">The font to draw with, or null for the default.</param>
    /// <param name="effects">Text effects to apply.</param>
    public static void Label(this Gui gui, string text,
        float? fontSize = null,
        bool selectable = false,
        bool centerInRect = false,
        Font? font = null,
        TextEffects? effects = null,
        IReadOnlyList<string>? classes = null, string? id = null)
    {
        ExcaliburStyles.Ensure(gui);
        using var scope = gui.StyledNode("label", classes, id).Enter();
        var size = fontSize ?? gui.CurrentNode.Scope.Get<LayoutNodeScopeTextSize>().Value;
        gui.DrawText(text, selectable, size, null, font, 0, centerInRect, clip: false, effects, layout: null);
    }
}
