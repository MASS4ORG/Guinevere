namespace Guinevere;

/// <summary>Builds Skia filter graphs whose native references retain their inputs.</summary>
static class RenderEffectFilters
{
    internal static SKImageFilter? Create(RenderEffect effect, SKImageFilter? input, bool runtime,
        List<string> diagnostics) => effect switch
        {
            RenderEffect.Blur e => SKImageFilter.CreateBlur(Positive(e.Radius), Positive(e.Radius), input),
            RenderEffect.Glow e => SKImageFilter.CreateDropShadow(0, 0, Positive(e.Radius), Positive(e.Radius),
                e.Color, input),
            RenderEffect.DropShadow e => SKImageFilter.CreateDropShadow(Finite(e.X), Finite(e.Y),
                Positive(e.Radius), Positive(e.Radius), e.Color, input),
            RenderEffect.Pixelate e => Pixelate(MathF.Max(1, Positive(e.Size)), input),
            RenderEffect.ChromaticAberration e => Chromatic(Finite(e.X), Finite(e.Y), input),
            RenderEffect.Sharpen e => Sharpen(Positive(e.Amount), input),
            _ => CustomOrColor(effect, input, runtime, diagnostics),
        };

    static SKImageFilter? CustomOrColor(RenderEffect effect, SKImageFilter? input, bool runtime,
        List<string> diagnostics) => effect switch
        {
            RenderEffect.CompiledColorFilter e => SKImageFilter.CreateColorFilter(e.Filter, input),
            RenderEffect.RuntimeColorFilter e => Runtime(e.Source, input, runtime, diagnostics),
            _ => Color(effect, input, diagnostics),
        };

    static float Finite(float value) => float.IsFinite(value) ? value : 0;
    static float Positive(float value) => MathF.Max(0, Finite(value));

    static SKImageFilter Pixelate(float size, SKImageFilter? input)
    {
        var down = SKMatrix.CreateScale(1 / size, 1 / size);
        var up = SKMatrix.CreateScale(size, size);
        var nearest = new SKSamplingOptions(SKFilterMode.Nearest);
        using var small = SKImageFilter.CreateMatrix(in down, nearest, input);
        return SKImageFilter.CreateMatrix(in up, nearest, small);
    }

    static SKImageFilter Sharpen(float amount, SKImageFilter? input) =>
        SKImageFilter.CreateMatrixConvolution(new SKSizeI(3, 3),
            [0, -amount, 0, -amount, 1 + 4 * amount, -amount, 0, -amount, 0],
            1, 0, new SKPointI(1, 1), SKShaderTileMode.Decal, false, input);

    static SKImageFilter Chromatic(float x, float y, SKImageFilter? input)
    {
        using var red = Channel(0, input);
        using var green = Channel(1, input);
        using var blue = Channel(2, input);
        using var left = SKImageFilter.CreateOffset(x, y, red);
        using var right = SKImageFilter.CreateOffset(-x, -y, blue);
        using var rg = SKImageFilter.CreateBlendMode(SKBlendMode.Plus, left, green);
        using var rgb = SKImageFilter.CreateBlendMode(SKBlendMode.Plus, rg, right);
        return SKImageFilter.CreateBlendMode(SKBlendMode.SrcIn, input, rgb);
    }

    static SKImageFilter Channel(int channel, SKImageFilter? input)
    {
        var matrix = new float[20];
        matrix[channel * 6] = 1;
        matrix[18] = 1;
        using var color = SKColorFilter.CreateColorMatrix(matrix);
        return SKImageFilter.CreateColorFilter(color, input);
    }

    static SKImageFilter? Runtime(string source, SKImageFilter? input, bool runtime, List<string> diagnostics)
    {
        if (!runtime)
        {
            diagnostics.Add("Runtime color filters require runtime shader compilation support.");
            return input;
        }
        using var effect = SKRuntimeEffect.CreateColorFilter(source, out var error);
        if (effect is null)
        {
            diagnostics.Add(error ?? "The runtime color filter could not be compiled.");
            return input;
        }
        if (effect.UniformSize > 0 || effect.Children.Count > 0)
        {
            diagnostics.Add("The runtime color filter requires unbound uniforms or children.");
            return input;
        }
        using var color = effect.ToColorFilter();
        return SKImageFilter.CreateColorFilter(color, input);
    }

    static SKImageFilter? Color(RenderEffect effect, SKImageFilter? input, List<string> diagnostics)
    {
        var matrix = Matrix(effect);
        if (matrix is null)
        {
            diagnostics.Add($"Unsupported render effect: {effect.GetType().Name}.");
            return input;
        }
        using var color = SKColorFilter.CreateColorMatrix(matrix);
        return SKImageFilter.CreateColorFilter(color, input);
    }

    static float[]? Matrix(RenderEffect effect) => effect switch
    {
        RenderEffect.Saturation e => Saturation(Positive(e.Amount)),
        RenderEffect.Grayscale e => Saturation(1 - Math.Clamp(Finite(e.Amount), 0, 1)),
        RenderEffect.Brightness e => Scale(Positive(e.Amount), 0),
        RenderEffect.Contrast e => Scale(Positive(e.Amount), (1 - Positive(e.Amount)) / 2),
        RenderEffect.Invert e => Scale(1 - 2 * Math.Clamp(Finite(e.Amount), 0, 1),
            Math.Clamp(Finite(e.Amount), 0, 1)),
        RenderEffect.Tint e => Tint(e.Color, Math.Clamp(Finite(e.Amount), 0, 1)),
        RenderEffect.HueRotation e => Hue(Finite(e.Degrees)),
        _ => null,
    };

    static float[] Scale(float scale, float offset) =>
        [scale, 0, 0, 0, offset, 0, scale, 0, 0, offset, 0, 0, scale, 0, offset, 0, 0, 0, 1, 0];

    static float[] Tint(Color color, float amount)
    {
        var matrix = Scale(1 - amount, 0);
        matrix[4] = color.R / 255f * amount;
        matrix[9] = color.G / 255f * amount;
        matrix[14] = color.B / 255f * amount;
        return matrix;
    }

    static float[] Saturation(float amount)
    {
        var r = 0.2126f * (1 - amount);
        var g = 0.7152f * (1 - amount);
        var b = 0.0722f * (1 - amount);
        return [r + amount, g, b, 0, 0, r, g + amount, b, 0, 0, r, g, b + amount, 0, 0, 0, 0, 0, 1, 0];
    }

    static float[] Hue(float degrees)
    {
        var c = MathF.Cos(degrees * MathF.PI / 180);
        var s = MathF.Sin(degrees * MathF.PI / 180);
        return
        [
            .213f + c * .787f - s * .213f, .715f - c * .715f - s * .715f, .072f - c * .072f + s * .928f, 0, 0,
            .213f - c * .213f + s * .143f, .715f + c * .285f + s * .140f, .072f - c * .072f - s * .283f, 0, 0,
            .213f - c * .213f - s * .787f, .715f - c * .715f + s * .715f, .072f + c * .928f + s * .072f, 0, 0,
            0, 0, 0, 1, 0,
        ];
    }
}
