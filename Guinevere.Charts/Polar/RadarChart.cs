using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Draws axes and projected series for a radar chart.</summary>
public static class RadarChart
{
    /// <summary>Draws labels around the radar axes. Labels that would overlap or leave the bounds are omitted.</summary>
    /// <remarks>Supply one label per axis. The caller buffer records the bounds of the labels drawn, in draw order.</remarks>
    public static int DrawAxisLabels(Gui gui, Rect area, Rect labelBounds, ReadOnlySpan<Vector2> axes,
        ReadOnlySpan<string> labels, Span<Rect> placed, Color color, float size = 0, float gap = 12, Font? font = null)
    {
        if (gui.Pass != Pass.Pass2Render || !ChartArea.IsValid(area) || !ChartArea.IsValid(labelBounds) ||
            axes.Length != labels.Length || !float.IsFinite(gap) || gap < 0)
        {
            return 0;
        }
        var count = 0;
        var radius = Math.Min(area.W, area.H) / 2 + gap;
        var center = area.Center;
        for (var i = 0; i < axes.Length && count < placed.Length; i++)
        {
            if (!IsFinite(axes[i]) || string.IsNullOrEmpty(labels[i]))
            {
                continue;
            }
            var layout = gui.LayoutPlainText(labels[i], size: size, font: font);
            var anchor = center + axes[i] * radius;
            var bounds = new Rect(anchor.X - layout.Width / 2, anchor.Y - layout.Height / 2, layout.Width, layout.Height);
            if (!CanPlaceLabel(labelBounds, bounds, placed[..count]))
            {
                continue;
            }
            gui.DrawTextLayout(layout, bounds.Position, color);
            placed[count++] = bounds;
        }
        return count;
    }

    static bool CanPlaceLabel(Rect available, Rect label, ReadOnlySpan<Rect> placed)
    {
        if (!ChartArea.IsValid(label) || label.X < available.X || label.Y < available.Y ||
            label.X + label.W > available.X + available.W || label.Y + label.H > available.Y + available.H)
        {
            return false;
        }
        foreach (var existing in placed)
        {
            if (label.X < existing.X + existing.W && existing.X < label.X + label.W &&
                label.Y < existing.Y + existing.H && existing.Y < label.Y + label.H)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Draws spokes and evenly spaced polygonal rings using unit axis vertices in dimension order.</summary>
    public static void DrawGrid(Gui gui, Rect area, ReadOnlySpan<Vector2> axes, int rings, Color color)
    {
        if (gui.Pass != Pass.Pass2Render || !ChartArea.IsValid(area) || axes.Length < 3)
        {
            return;
        }
        var center = area.Center;
        var radius = Math.Min(area.W, area.H) / 2;
        foreach (var axis in axes)
        {
            if (IsFinite(axis))
            {
                gui.DrawLine(center, center + axis * radius, color);
            }
        }
        rings = Math.Clamp(rings, 0, 32);
        for (var ring = 1; ring <= rings; ring++)
        {
            DrawOutline(gui, center, radius * ring / rings, axes, color, 1);
        }
    }

    /// <summary>Draws the outline of one projected series. Missing vertices leave a break at that dimension.</summary>
    public static void DrawSeries(Gui gui, Rect area, ReadOnlySpan<Vector2> vertices, Color color,
        float thickness = 1)
    {
        if (gui.Pass == Pass.Pass2Render && ChartArea.IsValid(area) && vertices.Length >= 3 &&
            float.IsFinite(thickness) && thickness > 0)
        {
            DrawOutline(gui, area.Center, Math.Min(area.W, area.H) / 2, vertices, color, thickness);
        }
    }

    /// <summary>Returns the nearest visible series vertex index within a pixel tolerance, or -1.</summary>
    public static int HitSeries(Rect area, ReadOnlySpan<Vector2> vertices, Vector2 pointer, float tolerance = 4)
    {
        if (!ChartArea.IsValid(area) || !ChartArea.Contains(area, pointer))
        {
            return -1;
        }
        var radius = Math.Min(area.W, area.H) / 2;
        return RadarProjection.HitTestVertex((pointer - area.Center) / radius, vertices, tolerance / radius);
    }

    static void DrawOutline(Gui gui, Vector2 center, float radius, ReadOnlySpan<Vector2> vertices, Color color,
        float thickness)
    {
        var previous = vertices[^1];
        foreach (var current in vertices)
        {
            if (IsFinite(previous) && IsFinite(current))
            {
                gui.DrawLine(center + previous * radius, center + current * radius, color, thickness);
            }
            previous = current;
        }
    }

    static bool IsFinite(Vector2 point) => float.IsFinite(point.X) && float.IsFinite(point.Y);
}
