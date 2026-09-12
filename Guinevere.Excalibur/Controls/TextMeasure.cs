namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Shorthands over the measurement <see cref="TextEditor"/> exposes, so the controls in this
    /// package keep reading the way they did before the editing logic moved into Guinevere.
    /// </summary>
    private static SKFont MeasuringFont(Gui gui, float fontSize) => TextEditor.MeasuringFont(gui, fontSize);

    private static float MeasureTextWidth(SKFont font, string text) => TextEditor.MeasureWidth(font, text);
}
