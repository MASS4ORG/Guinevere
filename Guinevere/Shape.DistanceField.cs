using System.Runtime.InteropServices;

namespace Guinevere;

static class ShapeDistanceField
{
    // Filled paths are sampled at up to 512 points per axis; smaller details than a grid cell can disappear.
    internal static SKPath Blend(SKPath first, SKPath second, float amount, float smoothness = 0)
    {
        var bounds = SKRect.Union(first.Bounds, second.Bounds);
        var longest = Math.Max(bounds.Width, bounds.Height);
        if (longest <= 0) return new SKPath();
        var step = Math.Max(longest / 480, Math.Min(1, longest / 64));
        var padding = Math.Max(step * 3, smoothness / 4 + step * 2);
        bounds.Inflate(padding, padding);
        step = Math.Max(step, Math.Max(bounds.Width, bounds.Height) / 510);
        var width = (int)Math.Ceiling(bounds.Width / step) + 1;
        var height = (int)Math.Ceiling(bounds.Height / step) + 1;
        var a = Sample(first, bounds, step, width, height);
        var b = Sample(second, bounds, step, width, height);
        for (var i = 0; i < a.Length; i++)
            a[i] = smoothness > 0 ? SmoothMinimum(a[i], b[i], smoothness) : a[i] * (1 - amount) + b[i] * amount;
        return Trace(a, bounds, step, width, height);
    }

    static float SmoothMinimum(float a, float b, float radius)
    {
        var h = Math.Max(radius - Math.Abs(a - b), 0) / radius;
        return Math.Min(a, b) - h * h * radius / 4;
    }

    static float[] Sample(SKPath path, SKRect bounds, float step, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = false };
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(1 / step);
        canvas.Translate(-bounds.Left + step / 2, -bounds.Top + step / 2);
        canvas.DrawPath(path, paint);
        var pixels = new byte[bitmap.ByteCount];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        var inside = new bool[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                inside[y * width + x] = pixels[y * bitmap.RowBytes + x] != 0;
        var toInside = Distance(inside, true, width, height);
        var toOutside = Distance(inside, false, width, height);
        for (var i = 0; i < inside.Length; i++)
            toInside[i] = inside[i]
                ? -(MathF.Sqrt(toOutside[i]) - 0.5f) * step
                : (MathF.Sqrt(toInside[i]) - 0.5f) * step;
        return toInside;
    }

    static float[] Distance(bool[] mask, bool target, int width, int height)
    {
        var values = new float[mask.Length];
        for (var i = 0; i < mask.Length; i++) values[i] = mask[i] == target ? 0 : 1e12f;
        var size = Math.Max(width, height);
        var input = new float[size];
        var output = new float[size];
        var sites = new int[size];
        var boundaries = new double[size + 1];
        for (var y = 0; y < height; y++)
        {
            Array.Copy(values, y * width, input, 0, width);
            Transform(input, output, sites, boundaries, width);
            Array.Copy(output, 0, values, y * width, width);
        }
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++) input[y] = values[y * width + x];
            Transform(input, output, sites, boundaries, height);
            for (var y = 0; y < height; y++) values[y * width + x] = output[y];
        }
        return values;
    }

    // The lower envelope of squared-distance parabolas gives an exact Euclidean transform in linear time.
    static void Transform(float[] input, float[] output, int[] sites, double[] boundaries, int count)
    {
        var last = 0;
        sites[0] = 0;
        boundaries[0] = double.NegativeInfinity;
        boundaries[1] = double.PositiveInfinity;
        for (var q = 1; q < count; q++)
        {
            var crossing = Intersection(input, q, sites[last]);
            while (crossing <= boundaries[last])
            {
                last--;
                crossing = Intersection(input, q, sites[last]);
            }
            sites[++last] = q;
            boundaries[last] = crossing;
            boundaries[last + 1] = double.PositiveInfinity;
        }
        last = 0;
        for (var q = 0; q < count; q++)
        {
            while (boundaries[last + 1] < q) last++;
            var delta = q - sites[last];
            output[q] = delta * delta + input[sites[last]];
        }
    }

    static double Intersection(float[] input, int q, int site) =>
        ((double)input[q] + q * q - input[site] - site * site) / (2 * (q - site));

    static SKPath Trace(float[] field, SKRect bounds, float step, int width, int height)
    {
        var vertices = new Dictionary<long, Vertex>();
        for (var y = 0; y < height - 1; y++)
            for (var x = 0; x < width - 1; x++)
            {
                var top = y * width + x;
                AddTriangle(vertices, field, top, top + 1, top + width + 1);
                AddTriangle(vertices, field, top, top + width + 1, top + width);
            }
        var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd };
        foreach (var (key, vertex) in vertices)
        {
            if (vertex.Visited) continue;
            var point = Point(key, field, bounds, step, width);
            builder.MoveTo(point);
            Walk(builder, vertices, field, bounds, step, width, key);
            builder.Close();
        }
        return builder.Detach();
    }

    static void AddTriangle(Dictionary<long, Vertex> vertices, float[] field, int a, int b, int c)
    {
        Span<long> crossings = stackalloc long[3];
        var count = 0;
        AddCrossing(field, a, b, crossings, ref count);
        AddCrossing(field, b, c, crossings, ref count);
        AddCrossing(field, c, a, crossings, ref count);
        if (count != 2) return;
        Connect(vertices, crossings[0], crossings[1]);
        Connect(vertices, crossings[1], crossings[0]);
    }

    static void AddCrossing(float[] field, int a, int b, Span<long> crossings, ref int count)
    {
        if ((field[a] < 0) == (field[b] < 0)) return;
        crossings[count++] = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
    }

    static void Connect(Dictionary<long, Vertex> vertices, long key, long neighbor)
    {
        if (!vertices.TryGetValue(key, out var vertex))
        {
            vertex = new Vertex();
            vertices.Add(key, vertex);
        }
        if (vertex.First == -1) vertex.First = neighbor;
        else vertex.Second = neighbor;
    }

    static void Walk(SKPathBuilder builder, Dictionary<long, Vertex> vertices, float[] field,
        SKRect bounds, float step, int width, long start)
    {
        var current = start;
        var previous = -1L;
        do
        {
            var vertex = vertices[current];
            vertex.Visited = true;
            var next = vertex.First == previous ? vertex.Second : vertex.First;
            if (next == -1 || next == start) return;
            builder.LineTo(Point(next, field, bounds, step, width));
            previous = current;
            current = next;
        } while (!vertices[current].Visited);
    }

    static SKPoint Point(long key, float[] field, SKRect bounds, float step, int width)
    {
        var a = (int)(key >> 32);
        var b = (int)key;
        var t = field[a] / (field[a] - field[b]);
        var x = a % width + (b % width - a % width) * t;
        var y = a / width + (b / width - a / width) * t;
        return new SKPoint(bounds.Left + x * step, bounds.Top + y * step);
    }

    sealed class Vertex
    {
        internal long First = -1;
        internal long Second = -1;
        internal bool Visited;
    }
}
