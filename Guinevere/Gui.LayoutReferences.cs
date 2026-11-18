namespace Guinevere;

public partial class Gui
{
    /// <summary>The frame generation; nodes stamped with an older one were not built this frame.</summary>
    internal int BuildGeneration { get; private set; }

    /// <summary>The current layout run; a node stamped with it already has its final rect for this run.</summary>
    internal int LayoutRun { get; set; }

    /// <summary>Node sizes read through <see cref="Size.PercentOf"/> during the current layout run.</summary>
    internal List<LayoutNode.ReferenceRead> ReferenceReads { get; } = [];

    /// <summary>Starts a frame generation, retiring every node not rebuilt in it.</summary>
    internal void AdvanceBuildGeneration() => BuildGeneration++;
}
