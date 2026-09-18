using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Guinevere;

/// <inheritdoc />
[Editor(
    "System.Drawing.Design.ColorEditor, System.Drawing.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
    "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
[Serializable]
[TypeConverter(
    "System.Drawing.ColorConverter, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
[TypeForwardedFrom("System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
public readonly struct Color : IEquatable<Color>
{
    readonly System.Drawing.Color _internalColor;

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

    public static implicit operator System.Drawing.Color(Color value) => value._internalColor;
    public static implicit operator Color(System.Drawing.Color value) => new(value);

    public static implicit operator SKColor(Color color) => new(color.R, color.G, color.B, color.A);

    Color(System.Drawing.Color color)
    {
        _internalColor = color;
    }

    public Color(Color color, float alpha)
    {
        FromArgb((int)(alpha * 255), color.B, color.G, color.R);
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

    public byte R => _internalColor.R;
    public byte G => _internalColor.G;
    public byte B => _internalColor.B;
    public byte A => _internalColor.A;
    public bool IsKnownColor => _internalColor.IsKnownColor;
    public bool IsEmpty => _internalColor.IsEmpty;
    public bool IsNamedColor => _internalColor.IsNamedColor;
    public bool IsSystemColor => _internalColor.IsSystemColor;
    public string Name => _internalColor.Name;

    public static Color FromArgb(int argb)
    {
        return System.Drawing.Color.FromArgb(argb);
    }

    public static Color FromArgb(int alpha, int red, int green, int blue)
    {
        return System.Drawing.Color.FromArgb(alpha, red, green, blue);
    }

    public static Color FromArgb(int alpha, Color baseColor)
    {
        return System.Drawing.Color.FromArgb(alpha, baseColor._internalColor);
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
        return _internalColor.GetBrightness();
    }

    public float GetHue()
    {
        return _internalColor.GetHue();
    }

    public float GetSaturation()
    {
        return _internalColor.GetSaturation();
    }

    public int ToArgb()
    {
        return _internalColor.ToArgb();
    }

    public KnownColor ToKnownColor()
    {
        return _internalColor.ToKnownColor();
    }

    public override string ToString()
    {
        return _internalColor.ToString();
    }

    public static bool operator ==(Color left, Color right) =>
        left._internalColor == right._internalColor;

    public static bool operator !=(Color left, Color right) =>
        !(left == right);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is System.Drawing.Color other && Equals(other);
    }

    public bool Equals(Color other)
    {
        return this == other;
    }

    public override int GetHashCode()
    {
        return _internalColor.GetHashCode();
    }

    #endregion
}
