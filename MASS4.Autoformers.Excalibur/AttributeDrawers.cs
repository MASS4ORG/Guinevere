namespace MASS4.Autoformers;

/// <summary>Draws a <c>[Title]</c> heading, optional subtitle and separating line above the field.</summary>
sealed class TitleDrawer : IAttributeDrawer
{
    internal static readonly TitleDrawer Instance = new();

    /// <summary>Outermost, so the heading sits above every other decoration.</summary>
    public int Order => -100;

    public void Draw(Gui gui, FormField field, Attribute attribute, string id, FormRenderContext context, Action next)
    {
        var title = (TitleAttribute)attribute;
        var style = new FormStyle(gui);

        using (gui.Node(-1, -1, $"{id}/title").ExpandWidth().Direction(Axis.Vertical).Gap(2f).Enter())
        {
            using (gui.Node(-1, style.RowHeight, $"{id}/title/text").ExpandWidth().ContentAlignY(0.5f).Enter())
                gui.DrawText(title.Title, style.FontSize + 1f, style.Ink, centerInRect: false,
                    effects: FormControls.Emphasis(title.Bold, style.Ink));

            if (title.Subtitle is { } subtitle)
                using (gui.Node(-1, style.FontSize + 4f, $"{id}/title/subtitle").ExpandWidth().Enter())
                    gui.DrawText(subtitle, style.FontSize - 1f, style.InkDim, centerInRect: false);

            if (title.HorizontalLine)
                using (gui.Node(-1, 1f, $"{id}/title/line").ExpandWidth().Enter())
                    if (gui.Pass == Pass.Pass2Render) gui.DrawBackgroundRect(style.Divider, 0f);

            next();
        }
    }
}

/// <summary>Draws an error under a <c>[Required]</c> field while its value is missing.</summary>
sealed class RequiredDrawer : IAttributeDrawer
{
    internal static readonly RequiredDrawer Instance = new();

    /// <summary>Innermost, so the error sits directly under the field it is about.</summary>
    public int Order => 100;

    public void Draw(Gui gui, FormField field, Attribute attribute, string id, FormRenderContext context, Action next)
    {
        using (gui.Node(-1, -1, $"{id}/required").ExpandWidth().Direction(Axis.Vertical).Gap(2f).Enter())
        {
            next();
            if (!IsMissing(field.GetValue())) return;

            var style = new FormStyle(gui);
            using (gui.Node(-1, style.RowHeight, $"{id}/required/error").ExpandWidth().Padding(6f, 0f, 6f, 0f)
                       .ContentAlignY(0.5f).Enter())
            {
                var negative = style.Negative;
                if (gui.Pass == Pass.Pass2Render)
                    gui.DrawBackgroundRect(Color.FromArgb(40, negative.R, negative.G, negative.B), 3f);
                gui.DrawText(Message(field, (RequiredAttribute)attribute), style.FontSize, negative,
                    centerInRect: false);
            }
        }
    }

    /// <summary>Null, an empty string or an empty collection.</summary>
    internal static bool IsMissing(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        System.Collections.ICollection collection => collection.Count == 0,
        _ => false,
    };

    internal static string Message(FormField field, RequiredAttribute required) =>
        required.Message ?? $"{field.Label} is required";
}

/// <summary>
/// Tints a <c>[GUIColor]</c> field: its text takes the color and its surfaces blend toward it. The tint is set on
/// the field's style scope, so nested content inherits it.
/// </summary>
sealed class GUIColorDrawer : IAttributeDrawer
{
    internal static readonly GUIColorDrawer Instance = new();

    /// <summary>Inside the title, around everything else.</summary>
    public int Order => -50;

    public void Draw(Gui gui, FormField field, Attribute attribute, string id, FormRenderContext context, Action next)
    {
        var tint = (GUIColorAttribute)attribute;
        var color = Color.FromArgb(Channel(tint.A), Channel(tint.R), Channel(tint.G), Channel(tint.B));

        // Read from the parent: the node keeps its scope across both passes, so reading inside would blend twice.
        var surface = new FormStyle(gui).Field;
        using (gui.Node(-1, -1, $"{id}/tint").ExpandWidth().Direction(Axis.Vertical).Enter())
        {
            gui.CurrentNodeScope.Set(ControlStyles.Value<ControlText, Color>(color));
            gui.CurrentNodeScope.Set(ControlStyles.Value<ControlSurface, Color>(Blend(surface, color, 0.25f)));
            next();
        }
    }

    static int Channel(float value) => (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);

    internal static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        Mix(from.A, to.A, amount), Mix(from.R, to.R, amount), Mix(from.G, to.G, amount), Mix(from.B, to.B, amount));

    static int Mix(byte from, byte to, float amount) => (int)MathF.Round(from + (to - from) * amount);
}
