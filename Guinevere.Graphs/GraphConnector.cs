namespace Guinevere.Graphs;

/// <summary>A cubic connector with conservative culling bounds and scale-independent world geometry.</summary>
public readonly record struct GraphConnector(GraphPoint Source, GraphPoint Target)
{
    double Tangent => Math.Max(60, Math.Abs(Target.X - Source.X) / 2);

    /// <summary>Bounds enclosing the endpoints and both control points.</summary>
    public GraphBounds Bounds
    {
        get
        {
            var left = Math.Min(Source.X, Target.X - Tangent);
            var right = Math.Max(Target.X, Source.X + Tangent);
            return new GraphBounds(left - 4, Math.Min(Source.Y, Target.Y) - 4,
                right - left + 8, Math.Abs(Target.Y - Source.Y) + 8);
        }
    }

    /// <summary>Returns a point on the curve, clamping the parameter to its endpoints.</summary>
    public GraphPoint Point(double t)
    {
        t = Math.Clamp(t, 0, 1);
        var u = 1 - t;
        return new GraphPoint(u * u * u * Source.X + 3 * u * u * t * (Source.X + Tangent)
            + 3 * u * t * t * (Target.X - Tangent) + t * t * t * Target.X,
            u * u * u * Source.Y + 3 * u * u * t * Source.Y + 3 * u * t * t * Target.Y + t * t * t * Target.Y);
    }

    /// <summary>Approximates distance to the curve for pointer selection.</summary>
    public double Distance(GraphPoint point)
    {
        var best = double.PositiveInfinity;
        var previous = Source;
        for (var i = 1; i <= 32; i++)
        {
            var next = Point(i / 32d);
            var dx = next.X - previous.X;
            var dy = next.Y - previous.Y;
            var length = dx * dx + dy * dy;
            var t = length > 0 ? Math.Clamp(((point.X - previous.X) * dx
                + (point.Y - previous.Y) * dy) / length, 0, 1) : 0;
            best = Math.Min(best, Math.Sqrt(Math.Pow(point.X - previous.X - t * dx, 2)
                + Math.Pow(point.Y - previous.Y - t * dy, 2)));
            previous = next;
        }
        return best;
    }

    /// <summary>Draws a connector in the current world-scaled scope relative to its bounds.</summary>
    public void Draw(Gui gui, Color color, float thickness = 2)
    {
        var origin = gui.CurrentNode.Rect.Position;
        var bounds = Bounds;
        var previous = new Vector2((float)(Source.X - bounds.X), (float)(Source.Y - bounds.Y)) + origin;
        for (var i = 1; i <= 32; i++)
        {
            var point = Point(i / 32d);
            var next = new Vector2((float)(point.X - bounds.X), (float)(point.Y - bounds.Y)) + origin;
            gui.DrawLine(previous, next, color, thickness);
            previous = next;
        }
    }
}
