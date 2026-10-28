namespace Guinevere.Graphs;

/// <summary>Colors for a document view's canvas, node cards, ports, links, badges and overlays.</summary>
public sealed record GraphTheme(
    GraphCanvasStyle Canvas,
    Color NodeBody,
    Color NodeHeader,
    Color NodeSelected,
    Color NodeBorder,
    Color Title,
    Color PortLabel,
    Color InputPort,
    Color OutputPort,
    Color Link,
    Color LinkSelected,
    Color LinkPreview,
    Color Badge,
    Color BadgeText,
    Color Warning,
    Color Error,
    Color Flow)
{
    /// <summary>The default dark palette, matching <see cref="GraphCanvasStyle.Default"/>.</summary>
    public static GraphTheme Dark { get; } = new(GraphCanvasStyle.Default,
        new Color(0x29384eff), new Color(0x213047ff), new Color(0x315c83ff), new Color(0x3d5270ff),
        Color.White, Color.LightSteelBlue, Color.LightSkyBlue, Color.MediumAquamarine,
        Color.LightSkyBlue, Color.Gold, new Color(0x80bfff99), new Color(0x0f1217dd), Color.White,
        new Color(0xf2c14eff), new Color(0xff6b6bff), new Color(0x7cffc4ff));

    /// <summary>A light paper palette with dark ink.</summary>
    public static GraphTheme Light { get; } = new(
        new GraphCanvasStyle(new Color(0xf4f1eaff), new Color(0xe4dfd3ff), new Color(0xcfc8b8ff),
            new Color(0x8a8270ff), new Color(0x1f6fd1ff), new Color(0xffffffee), new Color(0x8a8270ff),
            new Color(0x1f6fd1ff)),
        new Color(0xffffffff), new Color(0xe8e3d6ff), new Color(0xd7e7fbff), new Color(0xb8b09dff),
        new Color(0x1f2430ff), new Color(0x4b5263ff), new Color(0x1f6fd1ff), new Color(0x1a936fff),
        new Color(0x5b6478ff), new Color(0xd1495bff), new Color(0x1f6fd199), new Color(0x1f2430dd), Color.White,
        new Color(0xc77d00ff), new Color(0xc0392bff), new Color(0x1a936fff));
}

/// <summary>Zoom-dependent rendering detail; logical navigation and accessibility are unaffected.</summary>
public enum GraphLevelOfDetail
{
    /// <summary>Titles, ports with labels, badges, content and arrows.</summary>
    Full,
    /// <summary>Titles, port markers and badges without port labels or node content.</summary>
    Compact,
    /// <summary>Node silhouettes and thin links only; ports are neither drawn nor hit-tested.</summary>
    Outline
}

/// <summary>A port marker shape, chosen per port by the domain adapter.</summary>
public enum GraphPortShape
{
    /// <summary>A filled circle.</summary>
    Circle,
    /// <summary>A filled square.</summary>
    Square,
    /// <summary>A filled diamond.</summary>
    Diamond,
    /// <summary>A triangle pointing in the flow direction.</summary>
    Triangle
}
