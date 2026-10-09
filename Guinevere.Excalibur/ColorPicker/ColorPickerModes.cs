namespace Guinevere;

/// <summary>One numeric channel a <see cref="IColorPickerMode"/> exposes, such as red in [0, 255].</summary>
/// <param name="Label">Short label drawn before the field.</param>
/// <param name="Min">Lowest value.</param>
/// <param name="Max">Highest value.</param>
/// <param name="Format">Number format of the field.</param>
public readonly record struct ColorChannel(string Label, float Min, float Max, string Format = "0");

/// <summary>
/// A color model edited as numeric channels, such as RGB or HSL. Implement it to add a model to the picker's channel
/// row; alpha is a separate channel the picker adds itself.
/// </summary>
public interface IColorPickerMode
{
    /// <summary>Name shown on the mode switch.</summary>
    string Name { get; }

    /// <summary>The channels, in display order.</summary>
    IReadOnlyList<ColorChannel> Channels { get; }

    /// <summary>Writes the channel values of <paramref name="value"/> into <paramref name="channels"/>.</summary>
    /// <param name="value">The color being edited.</param>
    /// <param name="channels">One slot per entry of <see cref="Channels"/>.</param>
    void Read(Hsva value, Span<float> channels);

    /// <summary>Builds the edited color from channel values, keeping <paramref name="value"/>'s alpha.</summary>
    /// <param name="value">The color before the edit; supplies alpha and hue hints.</param>
    /// <param name="channels">One value per entry of <see cref="Channels"/>.</param>
    Hsva Write(Hsva value, ReadOnlySpan<float> channels);
}

/// <summary>The built-in <see cref="IColorPickerMode"/> implementations.</summary>
public static class ColorPickerModes
{
    /// <summary>Red, green and blue in [0, 255].</summary>
    public static IColorPickerMode Rgb { get; } = new RgbMode();

    /// <summary>Hue in degrees, saturation and value in percent.</summary>
    public static IColorPickerMode Hsv { get; } = new HsvMode();

    /// <summary>Hue in degrees, saturation and lightness in percent.</summary>
    public static IColorPickerMode Hsl { get; } = new HslMode();

    /// <summary>Linear-light red, green and blue in [0, 1], as shaders and HDR values use them.</summary>
    public static IColorPickerMode Linear { get; } = new LinearMode();

    /// <summary>The modes the default picker offers.</summary>
    public static IReadOnlyList<IColorPickerMode> Default { get; } = [Rgb, Hsv, Hsl];

    /// <summary>Converts an sRGB-encoded component to linear light.</summary>
    public static float SrgbToLinear(float value) =>
        value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

    /// <summary>Converts a linear-light component to sRGB encoding.</summary>
    public static float LinearToSrgb(float value) =>
        value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;

    sealed class RgbMode : IColorPickerMode
    {
        public string Name => "RGB";
        public IReadOnlyList<ColorChannel> Channels { get; } =
            [new("R", 0, 255), new("G", 0, 255), new("B", 0, 255)];

        public void Read(Hsva value, Span<float> channels)
        {
            var color = value.ToColor();
            channels[0] = color.R;
            channels[1] = color.G;
            channels[2] = color.B;
        }

        public Hsva Write(Hsva value, ReadOnlySpan<float> channels) =>
            Hsva.FromRgb(channels[0] / 255f, channels[1] / 255f, channels[2] / 255f, value.A, value.H, value.S);
    }

    sealed class HsvMode : IColorPickerMode
    {
        public string Name => "HSV";
        public IReadOnlyList<ColorChannel> Channels { get; } =
            [new("H", 0, 360), new("S", 0, 100), new("V", 0, 100)];

        public void Read(Hsva value, Span<float> channels)
        {
            channels[0] = value.H;
            channels[1] = value.S * 100f;
            channels[2] = value.V * 100f;
        }

        public Hsva Write(Hsva value, ReadOnlySpan<float> channels) =>
            new Hsva(channels[0] % 360f, channels[1] / 100f, channels[2] / 100f, value.A).Normalized();
    }

    sealed class HslMode : IColorPickerMode
    {
        public string Name => "HSL";
        public IReadOnlyList<ColorChannel> Channels { get; } =
            [new("H", 0, 360), new("S", 0, 100), new("L", 0, 100)];

        public void Read(Hsva value, Span<float> channels)
        {
            var lightness = value.V * (1f - value.S / 2f);
            var range = MathF.Min(lightness, 1f - lightness);
            channels[0] = value.H;
            channels[1] = (range > 0f ? (value.V - lightness) / range : value.S) * 100f;
            channels[2] = lightness * 100f;
        }

        public Hsva Write(Hsva value, ReadOnlySpan<float> channels)
        {
            var saturation = Math.Clamp(channels[1] / 100f, 0f, 1f);
            var lightness = Math.Clamp(channels[2] / 100f, 0f, 1f);
            var v = lightness + saturation * MathF.Min(lightness, 1f - lightness);
            var s = v > 0f ? 2f * (1f - lightness / v) : value.S;
            return new Hsva(channels[0] % 360f, s, v, value.A).Normalized();
        }
    }

    sealed class LinearMode : IColorPickerMode
    {
        public string Name => "Linear";
        public IReadOnlyList<ColorChannel> Channels { get; } =
            [new("R", 0, 1, "0.###"), new("G", 0, 1, "0.###"), new("B", 0, 1, "0.###")];

        public void Read(Hsva value, Span<float> channels)
        {
            var (r, g, b) = value.Rgb();
            channels[0] = SrgbToLinear(r);
            channels[1] = SrgbToLinear(g);
            channels[2] = SrgbToLinear(b);
        }

        public Hsva Write(Hsva value, ReadOnlySpan<float> channels) =>
            Hsva.FromRgb(LinearToSrgb(Math.Clamp(channels[0], 0f, 1f)), LinearToSrgb(Math.Clamp(channels[1], 0f, 1f)),
                LinearToSrgb(Math.Clamp(channels[2], 0f, 1f)), value.A, value.H, value.S);
    }
}
