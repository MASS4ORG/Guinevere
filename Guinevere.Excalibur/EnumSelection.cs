using System.Globalization;

namespace Guinevere;

/// <summary>Selection operations for enums, including zero, composite flags and signed backing types.</summary>
public static class EnumSelection
{
    /// <summary>Whether an option equals the value, or all its bits are present in a flag value.</summary>
    public static bool Contains(Enum value, Enum option)
    {
        Validate(value, option);
        if (!value.GetType().IsDefined(typeof(FlagsAttribute), false)) return value.Equals(option);
        var bits = Bits(option);
        return bits == 0 ? Bits(value) == 0 : (Bits(value) & bits) == bits;
    }

    /// <summary>Applies an edit while preserving unrelated flag bits; selecting zero clears the value.</summary>
    public static Enum Apply(Enum value, SelectionChange<Enum> change)
    {
        Validate(value, change.Item);
        if (!value.GetType().IsDefined(typeof(FlagsAttribute), false))
            return change.Selected ? change.Item : value;
        var bits = Bits(change.Item);
        if (bits == 0) return change.Selected ? (Enum)Enum.ToObject(value.GetType(), 0UL) : value;
        var next = change.Selected ? Bits(value) | bits : Bits(value) & ~bits;
        return (Enum)Enum.ToObject(value.GetType(), next);
    }

    static void Validate(Enum value, Enum option)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(option);
        if (value.GetType() != option.GetType())
            throw new ArgumentException("Value and option must have the same enum type.", nameof(option));
    }

    static ulong Bits(Enum value) => Type.GetTypeCode(Enum.GetUnderlyingType(value.GetType())) switch
    {
        TypeCode.SByte => unchecked((byte)Convert.ToSByte(value, CultureInfo.InvariantCulture)),
        TypeCode.Int16 => unchecked((ushort)Convert.ToInt16(value, CultureInfo.InvariantCulture)),
        TypeCode.Int32 => unchecked((uint)Convert.ToInt32(value, CultureInfo.InvariantCulture)),
        TypeCode.Int64 => unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)),
        _ => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
    };
}
