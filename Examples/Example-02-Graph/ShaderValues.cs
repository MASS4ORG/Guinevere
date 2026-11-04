using System.Text.Json;

namespace Example02;

/// <summary>A linear RGBA shader color.</summary>
/// <param name="R">Red.</param>
/// <param name="G">Green.</param>
/// <param name="B">Blue.</param>
/// <param name="A">Alpha.</param>
public readonly record struct Rgba(float R, float G, float B, float A)
{
    /// <summary>A grey with the same value in every channel, including alpha.</summary>
    public static Rgba Splat(float value) => new(value, value, value, value);

    /// <summary>Channel-wise product.</summary>
    public static Rgba operator *(Rgba a, Rgba b) => new(a.R * b.R, a.G * b.G, a.B * b.B, a.A * b.A);

    /// <summary>Scales every channel.</summary>
    public static Rgba operator *(Rgba a, float s) => new(a.R * s, a.G * s, a.B * s, a.A * s);

    /// <summary>Linear interpolation from <paramref name="a"/> (t = 0) to <paramref name="b"/> (t = 1).</summary>
    public static Rgba Lerp(Rgba a, Rgba b, float t) =>
        new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);

    /// <summary>Rotates the hue of an opaque color around the grey axis by whole 30° steps.</summary>
    public Rgba RotateHue(float steps)
    {
        var angle = steps * MathF.PI / 6;
        var (sin, cos) = MathF.SinCos(angle);
        var k = (1 - cos) / 3;
        var s = sin / MathF.Sqrt(3);
        return new(
            R * (cos + k) + G * (k - s) + B * (k + s),
            R * (k + s) + G * (cos + k) + B * (k - s),
            R * (k - s) + G * (k + s) + B * (cos + k),
            A);
    }

    /// <summary>Packs the clamped color for drawing.</summary>
    public uint Pack() => NodePreview.Pack(R, G, B, A);
}

/// <summary>Texture coordinates of a shaded fragment.</summary>
/// <param name="U">Horizontal coordinate in [0, 1).</param>
/// <param name="V">Vertical coordinate in [0, 1).</param>
public readonly record struct Uv(float U, float V);

/// <summary>Evaluation-context feature carrying the fragment being shaded.</summary>
public sealed class Fragment
{
    /// <summary>Texture coordinates of the fragment.</summary>
    public Uv Uv { get; set; }
}

/// <summary>Payload of a scalar constant or the checker's tiles per side.</summary>
/// <param name="Value">The constant.</param>
public sealed record ScalarPayload(float Value);

/// <summary>Payload of a color constant.</summary>
/// <param name="Value">The constant.</param>
public sealed record ColorPayload(Rgba Value);

/// <summary>Reflection-free JSON for the shader domain's values and payloads.</summary>
static class ShaderJson
{
    public static IValueSerializer Scalar { get; } = new Serializer(
        static (writer, value) => writer.WriteNumberValue((float)value!), static element => element.GetSingle());

    public static IValueSerializer Color { get; } = new Serializer(
        static (writer, value) => WriteColor(writer, (Rgba)value!), static element => ReadColor(element));

    public static IValueSerializer ScalarPayload { get; } = new Serializer(
        static (writer, value) => writer.WriteNumberValue(((ScalarPayload)value!).Value),
        static element => new ScalarPayload(element.GetSingle()));

    public static IValueSerializer ColorPayload { get; } = new Serializer(
        static (writer, value) => WriteColor(writer, ((ColorPayload)value!).Value),
        static element => new ColorPayload(ReadColor(element)));

    static void WriteColor(Utf8JsonWriter writer, Rgba color)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(color.R);
        writer.WriteNumberValue(color.G);
        writer.WriteNumberValue(color.B);
        writer.WriteNumberValue(color.A);
        writer.WriteEndArray();
    }

    static Rgba ReadColor(JsonElement element) =>
        new(element[0].GetSingle(), element[1].GetSingle(), element[2].GetSingle(), element[3].GetSingle());

    sealed class Serializer(Action<Utf8JsonWriter, object?> write, Func<JsonElement, object?> read) : IValueSerializer
    {
        public void Write(Utf8JsonWriter writer, object? value) => write(writer, value);

        public object? Read(JsonElement element) => read(element);
    }
}
