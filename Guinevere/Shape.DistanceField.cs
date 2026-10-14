using System.Buffers;

namespace Guinevere;

/// <summary>Combines filled paths through their signed distance fields, which path operations cannot express.</summary>
static class ShapeDistanceField
{
    const int MaxCells = 192;
    const float Far = 1e12f;

    /// <summary>
    /// Blends two filled paths by interpolating their distances, or by a smooth minimum when smoothness is positive.
    /// </summary>
    /// <remarks>
    /// Fields are sampled on at most about 192 cells per axis; anti-aliased edge coverage keeps the contour accurate to
    /// a fraction of a cell, but features thinner than one cell can still disappear.
    /// </remarks>
    internal static SKPath Blend(SKPath first, SKPath second, float amount, float smoothness = 0)
    {
        var bounds = SKRect.Union(first.Bounds, second.Bounds);
        var longest = Math.Max(bounds.Width, bounds.Height);
        if (longest <= 0)
        {
            return new SKPath();
        }

        var step = Math.Max(longest / MaxCells, Math.Min(1, longest / 64));
        var padding = Math.Max(step * 3, smoothness / 4 + step * 2);
        bounds.Inflate(padding, padding);
        step = Math.Max(step, Math.Max(bounds.Width, bounds.Height) / MaxCells);
        var width = (int)Math.Ceiling(bounds.Width / step) + 1;
        var height = (int)Math.Ceiling(bounds.Height / step) + 1;
        var size = width * height;

        var pool = ArrayPool<float>.Shared;
        var a = pool.Rent(size);
        var b = pool.Rent(size);
        var outer = pool.Rent(size);
        var inner = pool.Rent(size);
        try
        {
            var scratch = new Scratch(Math.Max(width, height));
            Sample(first, bounds, step, width, height, a, outer, inner, scratch);
            Sample(second, bounds, step, width, height, b, outer, inner, scratch);
            for (var i = 0; i < size; i++)
            {
                a[i] = smoothness > 0 ? SmoothMinimum(a[i], b[i], smoothness) : a[i] * (1 - amount) + b[i] * amount;
            }

            return Trace(a, bounds, step, width, height);
        }
        finally
        {
            pool.Return(a);
            pool.Return(b);
            pool.Return(outer);
            pool.Return(inner);
        }
    }

    static float SmoothMinimum(float a, float b, float radius)
    {
        var h = Math.Max(radius - Math.Abs(a - b), 0) / radius;
        return Math.Min(a, b) - h * h * radius / 4;
    }

    // Edge pixels start at their sub-cell offset from the contour, so a coarse grid still locates the boundary closely.
    static void Sample(SKPath path, SKRect bounds, float step, int width, int height,
        float[] field, float[] outer, float[] inner, Scratch scratch)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Alpha8, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(1 / step);
            canvas.Translate(-bounds.Left + step / 2, -bounds.Top + step / 2);
            canvas.DrawPath(path, paint);
        }
        var pixels = bitmap.GetPixelSpan();
        var rowBytes = bitmap.RowBytes;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                var coverage = pixels[y * rowBytes + x] / 255f;
                if (coverage >= 1)
                {
                    outer[i] = 0;
                    inner[i] = Far;
                }
                else if (coverage <= 0)
                {
                    outer[i] = Far;
                    inner[i] = 0;
                }
                else
                {
                    var offset = 0.5f - coverage;
                    outer[i] = offset > 0 ? offset * offset : 0;
                    inner[i] = offset < 0 ? offset * offset : 0;
                }
            }
        }

        Distance(outer, width, height, scratch);
        Distance(inner, width, height, scratch);
        for (var i = 0; i < width * height; i++)
        {
            field[i] = (MathF.Sqrt(outer[i]) - MathF.Sqrt(inner[i])) * step;
        }
    }

    static void Distance(float[] values, int width, int height, Scratch scratch)
    {
        for (var y = 0; y < height; y++)
        {
            Array.Copy(values, y * width, scratch.Input, 0, width);
            Transform(scratch, width);
            Array.Copy(scratch.Output, 0, values, y * width, width);
        }
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                scratch.Input[y] = values[y * width + x];
            }

            Transform(scratch, height);
            for (var y = 0; y < height; y++)
            {
                values[y * width + x] = scratch.Output[y];
            }
        }
    }

    // The lower envelope of squared-distance parabolas gives an exact Euclidean transform in linear time.
    static void Transform(Scratch s, int count)
    {
        var input = s.Input;
        var sites = s.Sites;
        var boundaries = s.Boundaries;
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
            while (boundaries[last + 1] < q)
            {
                last++;
            }

            var delta = q - sites[last];
            s.Output[q] = delta * delta + input[sites[last]];
        }
    }

    static double Intersection(float[] input, int q, int site) =>
        ((double)input[q] + q * q - input[site] - site * site) / (2 * (q - site));

    // Marching squares keyed by grid edge: horizontal edges use the index of their left sample, vertical edges
    // offset the index of their top sample by the sample count. Segments run from the edge where the walk enters the
    // fill to the edge where it leaves, so every crossed edge has exactly one successor.
    static SKPath Trace(float[] field, SKRect bounds, float step, int width, int height)
    {
        var size = width * height;
        var edges = size * 2;
        var next = ArrayPool<int>.Shared.Rent(edges);
        try
        {
            Array.Fill(next, -1, 0, edges);
            LinkSegments(field, next, width, height);
            var builder = new SKPathBuilder { FillType = SKPathFillType.EvenOdd };
            var points = new List<SKPoint>();
            for (var start = 0; start < edges; start++)
            {
                if (next[start] < 0)
                {
                    continue;
                }

                points.Clear();
                var current = start;
                while (current >= 0)
                {
                    points.Add(EdgePoint(current, field, bounds, step, width, size));
                    var following = next[current];
                    next[current] = -1;
                    current = following == start ? -1 : following;
                }
                AddContour(builder, points);
            }
            return builder.Detach();
        }
        finally
        {
            ArrayPool<int>.Shared.Return(next);
        }
    }

    static void LinkSegments(float[] field, int[] next, int width, int height)
    {
        var size = width * height;
        Span<int> ids = stackalloc int[4];
        Span<bool> enters = stackalloc bool[4];
        for (var y = 0; y < height - 1; y++)
        {
            for (var x = 0; x < width - 1; x++)
            {
                var i = y * width + x;
                var tl = field[i] < 0;
                var tr = field[i + 1] < 0;
                var br = field[i + width + 1] < 0;
                var bl = field[i + width] < 0;
                if (tl == tr && tr == br && br == bl)
                {
                    continue;
                }

                var count = 0;
                AddCrossing(tl, tr, i, ids, enters, ref count);
                AddCrossing(tr, br, size + i + 1, ids, enters, ref count);
                AddCrossing(br, bl, i + width, ids, enters, ref count);
                AddCrossing(bl, tl, size + i, ids, enters, ref count);
                if (count == 2)
                {
                    if (enters[0])
                    {
                        next[ids[0]] = ids[1];
                    }
                    else
                    {
                        next[ids[1]] = ids[0];
                    }

                    continue;
                }
                var joined = field[i] + field[i + 1] + field[i + width + 1] + field[i + width] < 0;
                for (var j = 0; j < 4; j++)
                {
                    if (enters[j])
                    {
                        next[ids[j]] = ids[joined ? (j + 3) & 3 : (j + 1) & 3];
                    }
                }
            }
        }
    }

    static void AddCrossing(bool from, bool to, int edge, Span<int> ids, Span<bool> enters, ref int count)
    {
        if (from == to)
        {
            return;
        }

        ids[count] = edge;
        enters[count++] = to;
    }

    static SKPoint EdgePoint(int edge, float[] field, SKRect bounds, float step, int width, int size)
    {
        var vertical = edge >= size;
        var a = vertical ? edge - size : edge;
        var b = vertical ? a + width : a + 1;
        var t = field[a] / (field[a] - field[b]);
        float x = a % width, y = a / width;
        if (vertical)
        {
            y += t;
        }
        else
        {
            x += t;
        }

        return new SKPoint(bounds.Left + x * step, bounds.Top + y * step);
    }

    // Quadratic curves through the segment midpoints turn the sampled contour into a smooth closed outline.
    static void AddContour(SKPathBuilder builder, List<SKPoint> points)
    {
        var count = points.Count;
        if (count < 3)
        {
            return;
        }

        builder.MoveTo(Midpoint(points[count - 1], points[0]));
        for (var i = 0; i < count; i++)
        {
            builder.QuadTo(points[i], Midpoint(points[i], points[(i + 1) % count]));
        }

        builder.Close();
    }

    static SKPoint Midpoint(SKPoint a, SKPoint b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    sealed class Scratch(int size)
    {
        internal readonly float[] Input = new float[size];
        internal readonly float[] Output = new float[size];
        internal readonly int[] Sites = new int[size];
        internal readonly double[] Boundaries = new double[size + 1];
    }
}
