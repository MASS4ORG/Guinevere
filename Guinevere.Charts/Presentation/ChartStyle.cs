namespace Guinevere.Charts;

/// <summary>Colors resolved for the manually drawn parts of a styled chart node.</summary>
/// <param name="Axis">Axis and plot border color.</param>
/// <param name="Grid">Grid line color.</param>
/// <param name="Label">Axis and legend text color.</param>
/// <param name="Selection">Selected item color.</param>
/// <param name="TooltipText">Tooltip foreground color.</param>
/// <param name="TooltipBackground">Tooltip background color.</param>
public readonly record struct ChartStyle(
    Color Axis, Color Grid, Color Label, Color Selection, Color TooltipText, Color TooltipBackground)
{
    /// <summary>Resolves PSS parts inside the current chart node.</summary>
    public static ChartStyle Resolve(Gui gui)
    {
        var axis = gui.ResolvePart("chart-axis").GetColor("border-color") ?? Color.Gray;
        var grid = gui.ResolvePart("chart-grid").GetColor("border-color") ?? Color.FromRgba(0x80808040);
        var text = gui.ResolvePart("chart-label").GetColor("color") ??
            gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        var selection = gui.ResolvePart("chart-selection").GetColor("background-color") ?? Color.DodgerBlue;
        var tooltip = gui.ResolvePart("chart-tooltip");
        return new ChartStyle(axis, grid, text, selection, tooltip.GetColor("color") ?? Color.White,
            tooltip.GetColor("background-color") ?? Color.FromRgba(0x202020ee));
    }
}
