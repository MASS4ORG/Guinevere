using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Guinevere;

/// <summary>A packed <c>0xRRGGBBAA</c> sRGB color with linear, unpremultiplied alpha.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Color : IEquatable<Color>
{
    readonly uint _rgba;

    public static implicit operator Color(uint rgba) => FromArgb(
        (byte)(rgba & 0xFF), // A
        (byte)((rgba >> 24) & 0xFF), // R
        (byte)((rgba >> 16) & 0xFF), // G
        (byte)(rgba >> 8) & 0xFF);

    public static implicit operator Color(int rgba) => FromArgb(
        (byte)rgba, // A (Alpha) - lowest byte
        (byte)(rgba >> 24), // R (Red) - highest byte
        (byte)(rgba >> 16), // G (Green)
        (byte)(rgba >> 8));

    public static implicit operator System.Drawing.Color(Color value) =>
        System.Drawing.Color.FromArgb(value.A, value.R, value.G, value.B);
    public static implicit operator Color(System.Drawing.Color value) => new(value);

    public static implicit operator SKColor(Color color) => new(color.R, color.G, color.B, color.A);

    Color(System.Drawing.Color color)
    {
        _rgba = Pack(color.R, color.G, color.B, color.A);
    }

    public Color(Color color, float alpha)
    {
        _rgba = Pack(color.R, color.G, color.B, (byte)(Math.Clamp(alpha, 0f, 1f) * 255f));
    }

    /// <summary>
    /// Linearly interpolates between two colors.
    /// </summary>
    /// <param name="start">The starting color.</param>
    /// <param name="end">The ending color.</param>
    /// <param name="t">Interpolation factor (0.0 = start, 1.0 = end).</param>
    /// <returns>The interpolated color.</returns>
    public static Color Lerp(Color start, Color end, float t)
    {
        // Clamp t between 0 and 1
        t = Math.Clamp(t, 0f, 1f);

        // Interpolate each component (A, R, G, B)
        var a = (byte)(start.A + (end.A - start.A) * t);
        var r = (byte)(start.R + (end.R - start.R) * t);
        var g = (byte)(start.G + (end.G - start.G) * t);
        var b = (byte)(start.B + (end.B - start.B) * t);

        return FromArgb(a, r, g, b);
    }

    #region System.Drawing.Color

    public static readonly Color Empty = new(default);

    // -------------------------------------------------------------------
    //  static list of "web" colors...
    //
    public static Color Transparent => System.Drawing.Color.Transparent;
    public static Color AliceBlue => System.Drawing.Color.AliceBlue;
    public static Color AntiqueWhite => System.Drawing.Color.AntiqueWhite;
    public static Color Aqua => System.Drawing.Color.Aqua;
    public static Color Aquamarine => System.Drawing.Color.Aquamarine;
    public static Color Azure => System.Drawing.Color.Azure;
    public static Color Beige => System.Drawing.Color.Beige;
    public static Color Bisque => System.Drawing.Color.Bisque;
    public static Color Black => System.Drawing.Color.Black;
    public static Color BlanchedAlmond => System.Drawing.Color.BlanchedAlmond;
    public static Color Blue => System.Drawing.Color.Blue;
    public static Color BlueViolet => System.Drawing.Color.BlueViolet;
    public static Color Brown => System.Drawing.Color.Brown;
    public static Color BurlyWood => System.Drawing.Color.BurlyWood;
    public static Color CadetBlue => System.Drawing.Color.CadetBlue;
    public static Color Chartreuse => System.Drawing.Color.Chartreuse;
    public static Color Chocolate => System.Drawing.Color.Chocolate;
    public static Color Coral => System.Drawing.Color.Coral;
    public static Color CornflowerBlue => System.Drawing.Color.CornflowerBlue;
    public static Color Cornsilk => System.Drawing.Color.Cornsilk;
    public static Color Crimson => System.Drawing.Color.Crimson;
    public static Color Cyan => System.Drawing.Color.Cyan;
    public static Color DarkBlue => System.Drawing.Color.DarkBlue;
    public static Color DarkCyan => System.Drawing.Color.DarkCyan;
    public static Color DarkGoldenrod => System.Drawing.Color.DarkGoldenrod;
    public static Color DarkGray => System.Drawing.Color.DarkGray;
    public static Color DarkGreen => System.Drawing.Color.DarkGreen;
    public static Color DarkKhaki => System.Drawing.Color.DarkKhaki;
    public static Color DarkMagenta => System.Drawing.Color.DarkMagenta;
    public static Color DarkOliveGreen => System.Drawing.Color.DarkOliveGreen;
    public static Color DarkOrange => System.Drawing.Color.DarkOrange;
    public static Color DarkOrchid => System.Drawing.Color.DarkOrchid;
    public static Color DarkRed => System.Drawing.Color.DarkRed;
    public static Color DarkSalmon => System.Drawing.Color.DarkSalmon;
    public static Color DarkSeaGreen => System.Drawing.Color.DarkSeaGreen;
    public static Color DarkSlateBlue => System.Drawing.Color.DarkSlateBlue;
    public static Color DarkSlateGray => System.Drawing.Color.DarkSlateGray;
    public static Color DarkTurquoise => System.Drawing.Color.DarkTurquoise;
    public static Color DarkViolet => System.Drawing.Color.DarkViolet;
    public static Color DeepPink => System.Drawing.Color.DeepPink;
    public static Color DeepSkyBlue => System.Drawing.Color.DeepSkyBlue;
    public static Color DimGray => System.Drawing.Color.DimGray;
    public static Color DodgerBlue => System.Drawing.Color.DodgerBlue;
    public static Color Firebrick => System.Drawing.Color.Firebrick;
    public static Color FloralWhite => System.Drawing.Color.FloralWhite;
    public static Color ForestGreen => System.Drawing.Color.ForestGreen;
    public static Color Fuchsia => System.Drawing.Color.Fuchsia;
    public static Color Gainsboro => System.Drawing.Color.Gainsboro;
    public static Color GhostWhite => System.Drawing.Color.GhostWhite;
    public static Color Gold => System.Drawing.Color.Gold;
    public static Color Goldenrod => System.Drawing.Color.Goldenrod;
    public static Color Gray => System.Drawing.Color.Gray;
    public static Color Green => System.Drawing.Color.Green;
    public static Color GreenYellow => System.Drawing.Color.GreenYellow;
    public static Color Honeydew => System.Drawing.Color.Honeydew;
    public static Color HotPink => System.Drawing.Color.HotPink;
    public static Color IndianRed => System.Drawing.Color.IndianRed;
    public static Color Indigo => System.Drawing.Color.Indigo;
    public static Color Ivory => System.Drawing.Color.Ivory;
    public static Color Khaki => System.Drawing.Color.Khaki;
    public static Color Lavender => System.Drawing.Color.Lavender;
    public static Color LavenderBlush => System.Drawing.Color.LavenderBlush;
    public static Color LawnGreen => System.Drawing.Color.LawnGreen;
    public static Color LemonChiffon => System.Drawing.Color.LemonChiffon;
    public static Color LightBlue => System.Drawing.Color.LightBlue;
    public static Color LightCoral => System.Drawing.Color.LightCoral;
    public static Color LightCyan => System.Drawing.Color.LightCyan;
    public static Color LightGoldenrodYellow => System.Drawing.Color.LightGoldenrodYellow;
    public static Color LightGreen => System.Drawing.Color.LightGreen;
    public static Color LightGray => System.Drawing.Color.LightGray;
    public static Color LightPink => System.Drawing.Color.LightPink;
    public static Color LightSalmon => System.Drawing.Color.LightSalmon;
    public static Color LightSeaGreen => System.Drawing.Color.LightSeaGreen;
    public static Color LightSkyBlue => System.Drawing.Color.LightSkyBlue;
    public static Color LightSlateGray => System.Drawing.Color.LightSlateGray;
    public static Color LightSteelBlue => System.Drawing.Color.LightSteelBlue;
    public static Color LightYellow => System.Drawing.Color.LightYellow;
    public static Color Lime => System.Drawing.Color.Lime;
    public static Color LimeGreen => System.Drawing.Color.LimeGreen;
    public static Color Linen => System.Drawing.Color.Linen;
    public static Color Magenta => System.Drawing.Color.Magenta;
    public static Color Maroon => System.Drawing.Color.Maroon;
    public static Color MediumAquamarine => System.Drawing.Color.MediumAquamarine;
    public static Color MediumBlue => System.Drawing.Color.MediumBlue;
    public static Color MediumOrchid => System.Drawing.Color.MediumOrchid;
    public static Color MediumPurple => System.Drawing.Color.MediumPurple;
    public static Color MediumSeaGreen => System.Drawing.Color.MediumSeaGreen;
    public static Color MediumSlateBlue => System.Drawing.Color.MediumSlateBlue;
    public static Color MediumSpringGreen => System.Drawing.Color.MediumSpringGreen;
    public static Color MediumTurquoise => System.Drawing.Color.MediumTurquoise;
    public static Color MediumVioletRed => System.Drawing.Color.MediumVioletRed;
    public static Color MidnightBlue => System.Drawing.Color.MidnightBlue;
    public static Color MintCream => System.Drawing.Color.MintCream;
    public static Color MistyRose => System.Drawing.Color.MistyRose;
    public static Color Moccasin => System.Drawing.Color.Moccasin;
    public static Color NavajoWhite => System.Drawing.Color.NavajoWhite;
    public static Color Navy => System.Drawing.Color.Navy;
    public static Color OldLace => System.Drawing.Color.OldLace;
    public static Color Olive => System.Drawing.Color.Olive;
    public static Color OliveDrab => System.Drawing.Color.OliveDrab;
    public static Color Orange => System.Drawing.Color.Orange;
    public static Color OrangeRed => System.Drawing.Color.OrangeRed;
    public static Color Orchid => System.Drawing.Color.Orchid;
    public static Color PaleGoldenrod => System.Drawing.Color.PaleGoldenrod;
    public static Color PaleGreen => System.Drawing.Color.PaleGreen;
    public static Color PaleTurquoise => System.Drawing.Color.PaleTurquoise;
    public static Color PaleVioletRed => System.Drawing.Color.PaleVioletRed;
    public static Color PapayaWhip => System.Drawing.Color.PapayaWhip;
    public static Color PeachPuff => System.Drawing.Color.PeachPuff;
    public static Color Peru => System.Drawing.Color.Peru;
    public static Color Pink => System.Drawing.Color.Pink;
    public static Color Plum => System.Drawing.Color.Plum;
    public static Color PowderBlue => System.Drawing.Color.PowderBlue;
    public static Color Purple => System.Drawing.Color.Purple;
    public static Color RebeccaPurple => System.Drawing.Color.RebeccaPurple;
    public static Color Red => System.Drawing.Color.Red;
    public static Color RosyBrown => System.Drawing.Color.RosyBrown;
    public static Color RoyalBlue => System.Drawing.Color.RoyalBlue;
    public static Color SaddleBrown => System.Drawing.Color.SaddleBrown;
    public static Color Salmon => System.Drawing.Color.Salmon;
    public static Color SandyBrown => System.Drawing.Color.SandyBrown;
    public static Color SeaGreen => System.Drawing.Color.SeaGreen;
    public static Color SeaShell => System.Drawing.Color.SeaShell;
    public static Color Sienna => System.Drawing.Color.Sienna;
    public static Color Silver => System.Drawing.Color.Silver;
    public static Color SkyBlue => System.Drawing.Color.SkyBlue;
    public static Color SlateBlue => System.Drawing.Color.SlateBlue;
    public static Color SlateGray => System.Drawing.Color.SlateGray;
    public static Color Snow => System.Drawing.Color.Snow;
    public static Color SpringGreen => System.Drawing.Color.SpringGreen;
    public static Color SteelBlue => System.Drawing.Color.SteelBlue;
    public static Color Tan => System.Drawing.Color.Tan;
    public static Color Teal => System.Drawing.Color.Teal;
    public static Color Thistle => System.Drawing.Color.Thistle;
    public static Color Tomato => System.Drawing.Color.Tomato;
    public static Color Turquoise => System.Drawing.Color.Turquoise;
    public static Color Violet => System.Drawing.Color.Violet;
    public static Color Wheat => System.Drawing.Color.Wheat;
    public static Color White => System.Drawing.Color.White;
    public static Color WhiteSmoke => System.Drawing.Color.WhiteSmoke;
    public static Color Yellow => System.Drawing.Color.Yellow;

    public static Color YellowGreen => System.Drawing.Color.YellowGreen;

    //
    //  end "web" colors
    // -------------------------------------------------------------------

    public byte R => (byte)(_rgba >> 24);
    public byte G => (byte)(_rgba >> 16);
    public byte B => (byte)(_rgba >> 8);
    public byte A => (byte)_rgba;
    public bool IsKnownColor => ((System.Drawing.Color)this).IsKnownColor;
    public bool IsEmpty => _rgba == 0;
    public bool IsNamedColor => ((System.Drawing.Color)this).IsNamedColor;
    public bool IsSystemColor => ((System.Drawing.Color)this).IsSystemColor;
    public string Name => ((System.Drawing.Color)this).Name;

    /// <summary>Gets the packed <c>0xRRGGBBAA</c> sRGB value.</summary>
    public uint Rgba => _rgba;

    /// <summary>Parses CSS hexadecimal colors in RGB, RGBA, RRGGBB or RRGGBBAA form, with an optional '#'.</summary>
    public static Color ParseHex(string value) => TryParseHex(value, out var color)
        ? color
        : throw new FormatException("Expected RGB, RGBA, RRGGBB or RRGGBBAA hexadecimal color data.");

    /// <summary>Attempts to parse a CSS hexadecimal color, with an optional leading '#'.</summary>
    public static bool TryParseHex(string? value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var hex = value.AsSpan().Trim();
        if (hex[0] == '#') hex = hex[1..];

        Span<char> normalized = stackalloc char[8];
        var normalizedLength = hex.Length;
        if (hex.Length is 3 or 4)
        {
            for (var i = 0; i < hex.Length; i++)
            {
                normalized[i * 2] = hex[i];
                normalized[i * 2 + 1] = hex[i];
            }
            normalizedLength *= 2;
        }
        else if (hex.Length is 6 or 8)
            hex.CopyTo(normalized);
        else
            return false;

        if (!uint.TryParse(normalized[..normalizedLength], NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out var packed))
            return false;

        if (normalizedLength == 6) packed = packed << 8 | byte.MaxValue;
        color = new Color(packed, true);
        return true;
    }

    public static Color FromArgb(int argb)
    {
        return FromArgb((argb >> 24) & 255, (argb >> 16) & 255, (argb >> 8) & 255, argb & 255);
    }

    public static Color FromArgb(int alpha, int red, int green, int blue)
    {
        if ((uint)alpha > byte.MaxValue) throw InvalidChannel(nameof(alpha));
        if ((uint)red > byte.MaxValue) throw InvalidChannel(nameof(red));
        if ((uint)green > byte.MaxValue) throw InvalidChannel(nameof(green));
        if ((uint)blue > byte.MaxValue) throw InvalidChannel(nameof(blue));
        return new Color(Pack((byte)red, (byte)green, (byte)blue, (byte)alpha), true);
    }

    public static Color FromArgb(int alpha, Color baseColor)
    {
        return FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
    }

    public static Color FromArgb(int red, int green, int blue)
    {
        return System.Drawing.Color.FromArgb(red, green, blue);
    }

    public static Color FromKnownColor(KnownColor color)
    {
        return System.Drawing.Color.FromKnownColor(color);
    }

    public static Color FromName(string name)
    {
        return System.Drawing.Color.FromName(name);
    }

    public float GetBrightness()
    {
        return ((System.Drawing.Color)this).GetBrightness();
    }

    public float GetHue()
    {
        return ((System.Drawing.Color)this).GetHue();
    }

    public float GetSaturation()
    {
        return ((System.Drawing.Color)this).GetSaturation();
    }

    public int ToArgb()
    {
        return unchecked((int)((uint)A << 24 | (uint)R << 16 | (uint)G << 8 | B));
    }

    public KnownColor ToKnownColor()
    {
        return ((System.Drawing.Color)this).ToKnownColor();
    }

    public override string ToString()
    {
        return $"Color [A={A}, R={R}, G={G}, B={B}]";
    }

    public static bool operator ==(Color left, Color right) =>
        left._rgba == right._rgba;

    public static bool operator !=(Color left, Color right) =>
        !(left == right);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is Color other && Equals(other);
    }

    public bool Equals(Color other)
    {
        return this == other;
    }

    public override int GetHashCode()
    {
        return _rgba.GetHashCode();
    }

    #endregion

    Color(uint rgba, bool _) => _rgba = rgba;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint Pack(byte red, byte green, byte blue, byte alpha) =>
        (uint)(red << 24 | green << 16 | blue << 8 | alpha);

    static ArgumentException InvalidChannel(string name) =>
        new("Channel must be between 0 and 255.", name);
}
