using Guinevere;

namespace Example_01;

/// <summary>
/// The colors every demo draws with. Demos receive the active palette as an argument, so switching
/// <see cref="Dark"/> and <see cref="Light"/> restyles the whole tour without touching a demo.
/// </summary>
public sealed record ColorPalette(
    string Name,
    Color Background,
    Color Surface,
    Color SurfaceRaised,
    Color SurfaceHover,
    Color Border,
    Color Text,
    Color TextDim,
    Color Accent,
    Color AccentText,
    Color Positive,
    Color Negative,
    Color Warning,
    Color Track,
    Color[] Series)
{
    /// <summary>The dark theme.</summary>
    public static readonly ColorPalette Dark = new(
        "Dark",
        Background: Rgb(0x111722), Surface: Rgb(0x1a2230), SurfaceRaised: Rgb(0x222c3e), SurfaceHover: Rgb(0x2d3a52),
        Border: Rgb(0x354158), Text: Rgb(0xf1f4fa), TextDim: Rgb(0xa3aec2), Accent: Rgb(0x3d8bfd),
        AccentText: Rgb(0xffffff), Positive: Rgb(0x3ecf8e), Negative: Rgb(0xef5d5d), Warning: Rgb(0xf5b84a),
        Track: Rgb(0x151c28),
        Series: [Rgb(0x3d8bfd), Rgb(0x3ecf8e), Rgb(0xf5b84a), Rgb(0xef5d5d), Rgb(0xb07cf5), Rgb(0x35c7d8)]);

    /// <summary>The light theme.</summary>
    public static readonly ColorPalette Light = new(
        "Light",
        Background: Rgb(0xe9edf5), Surface: Rgb(0xffffff), SurfaceRaised: Rgb(0xf3f5fa), SurfaceHover: Rgb(0xe1e6ef),
        Border: Rgb(0xccd2dd), Text: Rgb(0x1e2634), TextDim: Rgb(0x525c6e), Accent: Rgb(0x7157d9),
        AccentText: Rgb(0xffffff), Positive: Rgb(0x1f9d63), Negative: Rgb(0xd23f3f), Warning: Rgb(0xc98512),
        Track: Rgb(0xdfe4ee),
        Series: [Rgb(0x7157d9), Rgb(0x1f9d63), Rgb(0xc98512), Rgb(0xd23f3f), Rgb(0x2f7fd8), Rgb(0x1c9fb0)]);

    /// <summary>Returns the series color for <paramref name="index"/>, wrapping around the list.</summary>
    public Color Pick(int index) => Series[index % Series.Length];

    /// <summary>Formats <paramref name="color"/> as the <c>#rrggbbaa</c> literal a stylesheet understands.</summary>
    public static string Hex(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";

    static Color Rgb(uint rgb) => Color.FromRgba((rgb << 8) | 0xFF);
}
