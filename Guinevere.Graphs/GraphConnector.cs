namespace Guinevere.Graphs;

/// <summary>How a link travels between its source and target attachment points.</summary>
public enum GraphLinkRouting
{
    /// <summary>A horizontal-tangent cubic curve.</summary>
    Bezier,
    /// <summary>A single straight segment.</summary>
    Straight,
    /// <summary>Horizontal and vertical segments, with stubs around backward links.</summary>
    Orthogonal
}

/// <summary>A routed connector with conservative culling bounds and scale-independent world geometry.</summary>
public readonly record struct GraphConnector(
    GraphPoint Source,
    GraphPoint Target,
    GraphLinkRouting Routing = GraphLinkRouting.Bezier)
{
    const int CurveSegments = 32;
    const double Stub = 24;
    readonly GraphPoint[]? _route;

    /// <summary>Creates an explicit segmented route, shared by drawing, culling, interpolation and hit testing.</summary>
    public GraphConnector(GraphPoint source, GraphPoint target, IEnumerable<GraphPoint> waypoints)
        : this(source, target, GraphLinkRouting.Orthogonal)
    {
        _route = [source, .. waypoints, target];
        foreach (var point in _route)
        {
            GraphBounds.RequireFinite(point.X, point.Y);
        }
    }

    double Tangent => Math.Max(60, Math.Abs(Target.X - Source.X) / 2);

    /// <summary>Bounds enclosing the endpoints, the route and any curve control points.</summary>
    public GraphBounds Bounds
    {
        get
        {
            if (_route is not null)
            {
                var routeLeft = _route.Min(static p => p.X);
                var routeTop = _route.Min(static p => p.Y);
                return new GraphBounds(routeLeft - 4, routeTop - 4, _route.Max(static p => p.X) - routeLeft + 8,
                    _route.Max(static p => p.Y) - routeTop + 8);
            }
            double left = Math.Min(Source.X, Target.X), right = Math.Max(Source.X, Target.X);
            double top = Math.Min(Source.Y, Target.Y), bottom = Math.Max(Source.Y, Target.Y);
            if (Routing == GraphLinkRouting.Bezier)
            {
                left = Math.Min(left, Target.X - Tangent);
                right = Math.Max(right, Source.X + Tangent);
            }
            else if (Routing == GraphLinkRouting.Orthogonal)
            {
                left = Math.Min(left, Target.X - Stub);
                right = Math.Max(right, Source.X + Stub);
            }
            return new GraphBounds(left - 4, top - 4, right - left + 8, bottom - top + 8);
        }
    }

    /// <summary>The route as connected world points; curves are sampled finely enough for drawing and hit tests.</summary>
    public GraphPoint[] Polyline()
    {
        if (_route is not null)
        {
            return [.. _route];
        }
        switch (Routing)
        {
            case GraphLinkRouting.Straight:
                return [Source, Target];
            case GraphLinkRouting.Orthogonal:
                return OrthogonalRoute();
            default:
                var points = new GraphPoint[CurveSegments + 1];
                for (var i = 0; i <= CurveSegments; i++)
                {
                    points[i] = CurvePoint((double)i / CurveSegments);
                }
                return points;
        }
    }

    /// <summary>The route's world length.</summary>
    public double Length
    {
        get
        {
            var points = Polyline();
            var length = 0d;
            for (var i = 1; i < points.Length; i++)
            {
                length += Distance(points[i - 1], points[i]);
            }
            return length;
        }
    }

    /// <summary>Returns a point on the route, clamping the parameter to its endpoints.</summary>
    /// <remarks>Curves use their cubic parameter; segmented routes use the fraction of their length.</remarks>
    public GraphPoint Point(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return _route is null && Routing == GraphLinkRouting.Bezier ? CurvePoint(t) : PointAlong(Polyline(), t);
    }

    /// <summary>Distance from a point to the route, independent of stroke width, for pointer selection.</summary>
    public double Distance(GraphPoint point)
    {
        var points = Polyline();
        var best = Distance(point, points[0]);
        for (var i = 1; i < points.Length; i++)
        {
            best = Math.Min(best, SegmentDistance(point, points[i - 1], points[i]));
        }
        return best;
    }

    /// <summary>Draws a solid connector in the current world-scaled scope relative to its bounds.</summary>
    public void Draw(Gui gui, Color color, float thickness = 2) => Draw(gui, new GraphLinkStyle(color, thickness));

    /// <summary>Draws a styled connector, optionally dashed and arrowed, relative to the current scope's bounds.</summary>
    public void Draw(Gui gui, GraphLinkStyle style)
    {
        var origin = gui.CurrentNode.Rect.Position;
        var bounds = Bounds;
        var points = Polyline();
        var local = new Vector2[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            local[i] = new Vector2((float)(points[i].X - bounds.X), (float)(points[i].Y - bounds.Y)) + origin;
        }
        if (style.Dash > 0 || style.DashPattern is { Count: > 0 })
        {
            DrawDashed(gui, local, style);
        }
        else
        {
            for (var i = 1; i < local.Length; i++)
            {
                gui.DrawLine(local[i - 1], local[i], style.Color, style.Thickness);
            }
        }
        if (style.Arrow)
        {
            DrawArrow(gui, local, style);
        }
    }

    GraphPoint CurvePoint(double t)
    {
        var u = 1 - t;
        return new GraphPoint(u * u * u * Source.X + 3 * u * u * t * (Source.X + Tangent)
            + 3 * u * t * t * (Target.X - Tangent) + t * t * t * Target.X,
            u * u * u * Source.Y + 3 * u * u * t * Source.Y + 3 * u * t * t * Target.Y + t * t * t * Target.Y);
    }

    GraphPoint[] OrthogonalRoute()
    {
        if (Target.X - Source.X >= Stub * 2)
        {
            var middle = (Source.X + Target.X) / 2;
            return [Source, new(middle, Source.Y), new(middle, Target.Y), Target];
        }
        var y = (Source.Y + Target.Y) / 2;
        var exit = Source.X + Stub;
        var entry = Target.X - Stub;
        return [Source, new(exit, Source.Y), new(exit, y), new(entry, y), new(entry, Target.Y), Target];
    }

    static GraphPoint PointAlong(GraphPoint[] points, double t)
    {
        var total = 0d;
        for (var i = 1; i < points.Length; i++)
        {
            total += Distance(points[i - 1], points[i]);
        }
        var remaining = total * t;
        for (var i = 1; i < points.Length; i++)
        {
            var segment = Distance(points[i - 1], points[i]);
            if (remaining <= segment && segment > 0)
            {
                var f = remaining / segment;
                return new GraphPoint(points[i - 1].X + (points[i].X - points[i - 1].X) * f,
                    points[i - 1].Y + (points[i].Y - points[i - 1].Y) * f);
            }
            remaining -= segment;
        }
        return points[^1];
    }

    static void DrawDashed(Gui gui, Vector2[] points, GraphLinkStyle style)
    {
        var pattern = style.DashPattern ?? [style.Dash, style.Dash];
        if (pattern.Any(static value => !float.IsFinite(value) || value < 0) || pattern.Sum() <= 0)
        {
            return;
        }
        var count = pattern.Count % 2 == 0 ? pattern.Count : pattern.Count * 2;
        var period = pattern.Sum() * (count / pattern.Count);
        var phase = ((-style.DashOffset % period) + period) % period;
        for (var i = 1; i < points.Length; i++)
        {
            var start = points[i - 1];
            var direction = points[i] - start;
            var length = direction.Length();
            if (length <= 0)
            {
                continue;
            }
            direction /= length;
            DrawDashSegment(gui, start, direction, length, phase, period, count, pattern, style);
            phase = (phase + length) % period;
        }
    }

    static void DrawDashSegment(Gui gui, Vector2 start, Vector2 direction, float length, float phase,
        float period, int count, IReadOnlyList<float> pattern, GraphLinkStyle style)
    {
        for (var at = -phase; at < length; at += period)
        {
            var position = at;
            for (var dash = 0; dash < count; dash++)
            {
                var next = position + pattern[dash % pattern.Count];
                var from = Math.Max(0, position);
                var to = Math.Min(length, next);
                if (dash % 2 == 0 && to > from)
                {
                    gui.DrawLine(start + direction * from, start + direction * to, style.Color, style.Thickness);
                }
                position = next;
            }
        }
    }

    static void DrawArrow(Gui gui, Vector2[] points, GraphLinkStyle style)
    {
        var tip = points[^1];
        var index = points.Length - 2;
        while (index > 0 && Vector2.DistanceSquared(points[index], tip) < 1e-6f)
        {
            index--;
        }
        var direction = tip - points[index];
        if (direction.LengthSquared() < 1e-6f)
        {
            return;
        }
        direction = Vector2.Normalize(direction);
        var size = style.ArrowSize;
        var normal = new Vector2(-direction.Y, direction.X) * size * 0.5f;
        var back = tip - direction * size;
        gui.DrawTriangleFilled(tip, back + normal, back - normal, style.Color);
    }

    static double SegmentDistance(GraphPoint point, GraphPoint a, GraphPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = dx * dx + dy * dy;
        var t = length > 0 ? Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / length, 0, 1) : 0;
        return Distance(point, new GraphPoint(a.X + t * dx, a.Y + t * dy));
    }

    static double Distance(GraphPoint a, GraphPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>Stroke appearance of a link; dash and offset are in world units, so a moving offset animates flow.</summary>
/// <param name="Color">The stroke and arrow color.</param>
/// <param name="Thickness">The stroke width in world units.</param>
/// <param name="Arrow">Draws an arrowhead at the target.</param>
/// <param name="Dash">Dash and gap length; zero draws a solid stroke.</param>
/// <param name="DashOffset">Distance the dash pattern has advanced toward the target.</param>
/// <param name="DashPattern">Alternating dash/gap lengths, with odd patterns repeated as in SVG.</param>
/// <param name="ArrowSize">Arrowhead length in world units.</param>
public readonly record struct GraphLinkStyle(
    Color Color,
    float Thickness = 2,
    bool Arrow = false,
    float Dash = 0,
    float DashOffset = 0,
    IReadOnlyList<float>? DashPattern = null,
    float ArrowSize = 10);
