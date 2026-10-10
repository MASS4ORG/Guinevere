namespace Guinevere;

public static partial class ExcaliburStyles
{
    /// <summary>
    /// Applies the sheets' <c>$control-field-height</c>, <c>$control-compact-height</c> and
    /// <c>$control-indicator-size</c> tokens as the current scope's control metrics, so a theme sets the density of every
    /// control that keeps its default size. Call it at the root every frame, before building controls;
    /// <see cref="SetTheme"/> does. Tokens a sheet leaves out keep <see cref="ControlMetrics"/>.
    /// </summary>
    /// <param name="gui">The GUI whose sheets define the tokens.</param>
    public static void ApplyMetrics(Gui gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        Ensure(gui);
        if (gui.LayoutNodeScopeStack.Count == 0) return;
        var scope = gui.CurrentNodeScope;
        if (gui.StyleSheets.GetTokenLength("control-field-height") is { } field)
            scope.Set(ControlStyles.Value<ControlFieldHeight, float>(field));
        if (gui.StyleSheets.GetTokenLength("control-compact-height") is { } compact)
            scope.Set(ControlStyles.Value<ControlCompactHeight, float>(compact));
        if (gui.StyleSheets.GetTokenLength("control-indicator-size") is { } indicator)
            scope.Set(ControlStyles.Value<ControlIndicatorSize, float>(indicator));
    }
}
