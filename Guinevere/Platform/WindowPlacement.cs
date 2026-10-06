namespace Guinevere;

/// <summary>Fits an initial client window inside a monitor's usable desktop area.</summary>
public static class WindowPlacement
{
    /// <summary>Selects the area with the largest window overlap, or the nearest area for an offscreen window.</summary>
    public static int SelectMonitor(Rect window, ReadOnlySpan<Rect> areas)
    {
        var selected = -1;
        var largestOverlap = -1f;
        var nearest = float.PositiveInfinity;
        var center = window.Position + window.Size * 0.5f;
        for (var i = 0; i < areas.Length; i++)
        {
            var area = areas[i];
            if (area.W <= 0 || area.H <= 0) continue;
            var overlap = Math.Max(0, Math.Min(window.X + window.W, area.X + area.W) - Math.Max(window.X, area.X))
                * Math.Max(0, Math.Min(window.Y + window.H, area.Y + area.H) - Math.Max(window.Y, area.Y));
            var closest = new Vector2(Math.Clamp(center.X, area.X, area.X + area.W),
                Math.Clamp(center.Y, area.Y, area.Y + area.H));
            var distance = Vector2.DistanceSquared(center, closest);
            if (overlap < largestOverlap || (overlap == largestOverlap && distance >= nearest)) continue;
            selected = i;
            largestOverlap = overlap;
            nearest = distance;
        }
        return selected;
    }

    /// <summary>Clamps the size and centers the window without obscuring its application bar.</summary>
    public static Rect Center(Vector2 size, Rect workArea)
    {
        var width = Math.Clamp(size.X, 1, Math.Max(1, workArea.W));
        var height = Math.Clamp(size.Y, 1, Math.Max(1, workArea.H));
        return new Rect(workArea.X + (workArea.W - width) * 0.5f,
            workArea.Y + (workArea.H - height) * 0.5f, width, height);
    }
}
