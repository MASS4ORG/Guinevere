namespace Guinevere;

public partial class Gui
{
    /// <summary>Draws four reticle arms and an optional center dot; spread and size are destination pixels.</summary>
    public void DrawReticle(Vector2 center, Color color, float spread = 5f, float length = 8f,
        float thickness = 2f, float dotRadius = 0f)
    {
        spread = Math.Max(0f, spread);
        length = Math.Max(0f, length);
        DrawLine(center + new Vector2(spread, 0), center + new Vector2(spread + length, 0), color, thickness);
        DrawLine(center - new Vector2(spread, 0), center - new Vector2(spread + length, 0), color, thickness);
        DrawLine(center + new Vector2(0, spread), center + new Vector2(0, spread + length), color, thickness);
        DrawLine(center - new Vector2(0, spread), center - new Vector2(0, spread + length), color, thickness);
        if (dotRadius > 0f)
        {
            DrawCircle(center, dotRadius, color);
        }
    }

    /// <summary>Draws an arrow at a radius from a center; the angle is in degrees, clockwise from right.</summary>
    public void DrawDirection(Vector2 center, float angle, float radius, Color color, float size = 10f)
    {
        var radians = angle * MathF.PI / 180f;
        var forward = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        var side = new Vector2(-forward.Y, forward.X);
        var tip = center + forward * radius;
        DrawTriangle(tip, tip - forward * size + side * size * 0.5f,
            tip - forward * size - side * size * 0.5f, color);
    }
}
