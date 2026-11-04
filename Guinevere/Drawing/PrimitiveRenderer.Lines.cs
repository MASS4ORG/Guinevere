namespace Guinevere;

sealed partial class PrimitiveRenderer
{
    static bool TryLineRectangle(in PrimitiveCommand command, SKMatrix matrix, out SKRect rectangle)
    {
        rectangle = default;
        var length = (command.B - command.A).Length();
        if (!float.IsFinite(length) || length <= .0001f || !SupportsLineRectangle(matrix))
        {
            return false;
        }
        var half = MathF.Max(.5f, command.Thickness * .5f);
        if (command.A.Y == command.B.Y)
        {
            rectangle = new SKRect(MathF.Min(command.A.X, command.B.X), command.A.Y - half,
                MathF.Max(command.A.X, command.B.X), command.A.Y + half);
        }
        else if (command.A.X == command.B.X)
        {
            rectangle = new SKRect(command.A.X - half, MathF.Min(command.A.Y, command.B.Y),
                command.A.X + half, MathF.Max(command.A.Y, command.B.Y));
        }
        else
        {
            return false;
        }
        return PixelAligned(matrix.MapRect(rectangle));
    }

    static bool SupportsLineRectangle(SKMatrix matrix) => matrix.SkewX == 0 && matrix.SkewY == 0 &&
        matrix.Persp0 == 0 && matrix.Persp1 == 0 && matrix.Persp2 == 1;

    static bool PixelAligned(SKRect bounds) => PixelEdge(bounds.Left) && PixelEdge(bounds.Top) &&
        PixelEdge(bounds.Right) && PixelEdge(bounds.Bottom);

    static bool PixelEdge(float value) => float.IsFinite(value) && value == MathF.Truncate(value);
}
