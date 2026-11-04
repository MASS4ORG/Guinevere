namespace Guinevere;

public partial class Gui
{
    /// <summary>Command and direct primitive submission counts from the last complete GUI replay.</summary>
    public DrawStatistics RenderStatistics { get; private set; }

    /// <summary>Enables CPU timing of node collection/sorting and draw-list replay.</summary>
    public bool MeasureRenderTimings { get; set; }

    /// <summary>CPU time collecting and sorting nodes; zero when render timing is disabled.</summary>
    public TimeSpan RenderSortTime { get; private set; }

    /// <summary>CPU time replaying nodes, including clips and opacity; excludes backend flush/presentation.</summary>
    public TimeSpan RenderReplayTime { get; private set; }
}
