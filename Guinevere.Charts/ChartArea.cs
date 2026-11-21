using System.Numerics;

namespace Guinevere.Charts;

static class ChartArea
{
    internal static bool IsValid(Rect area) =>
        area.W > 0 && area.H > 0 && float.IsFinite(area.X) && float.IsFinite(area.Y) &&
        float.IsFinite(area.X + area.W) && float.IsFinite(area.Y + area.H);

    internal static bool Contains(Rect area, Vector2 point) =>
        point.X >= area.X && point.Y >= area.Y && point.X < area.X + area.W && point.Y < area.Y + area.H;
}
