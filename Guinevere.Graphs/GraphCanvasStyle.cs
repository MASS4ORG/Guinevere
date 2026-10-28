namespace Guinevere.Graphs;

/// <summary>Core color values which an application may populate from any optional styling system.</summary>
public sealed record GraphCanvasStyle(
    Color Background,
    Color MinorGrid,
    Color MajorGrid,
    Color Axis,
    Color Focus,
    Color MinimapBackground,
    Color MinimapNode,
    Color MinimapViewport)
{
    /// <summary>A usable palette independent of Excalibur.</summary>
    public static GraphCanvasStyle Default { get; } = new(
        new Color(0x1a1c21ff), new Color(0x2b3038ff), new Color(0x3d424dff), new Color(0x66738cff),
        new Color(0x66b3ffff), new Color(0x0f1217ff), new Color(0x66738cff), new Color(0x80bfffff));
}
