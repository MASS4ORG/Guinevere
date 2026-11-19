using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>
/// Compact storage of a <see cref="LayoutStyle"/>'s distances. Each slot keeps its pixel term and at most one other
/// term in two floats, with the other term's kind packed in three bits; a value with several non-pixel terms or node
/// references lives in a side array that is replaced, never changed, so copies of a style never share edits.
/// </summary>
public partial struct LayoutStyle
{
    const int WidthSlot = 0;
    const int HeightSlot = 1;
    const int MinWidthSlot = 2;
    const int MaxWidthSlot = 3;
    const int MinHeightSlot = 4;
    const int MaxHeightSlot = 5;
    internal const int AlignSelfSlot = 6;
    const int GapSlot = 7;
    const int MarginTopSlot = 8;
    const int MarginRightSlot = 9;
    const int MarginBottomSlot = 10;
    const int MarginLeftSlot = 11;
    const int PaddingTopSlot = 12;
    const int PaddingRightSlot = 13;
    const int PaddingBottomSlot = 14;
    const int PaddingLeftSlot = 15;
    internal const int AlignContentXSlot = 16;
    internal const int AlignContentYSlot = 17;
    const int AbsoluteXSlot = 18;
    const int AbsoluteYSlot = 19;
    const int SlotCount = 20;
    const int KindBits = 3;
    const ulong KindMask = (1UL << KindBits) - 1;

    /// <summary>Which term besides pixels a slot holds.</summary>
    enum TermKind : ulong
    {
        None = 0,
        Percentage = 1,
        Ratio = 2,
        Expand = 3,
        FitContent = 4,
        FitLargest = 5,
        Complex = 7
    }

    /// <summary>One float per slot, inline in the style.</summary>
    [InlineArray(SlotCount)]
    struct SlotFloats
    {
        float _first;
    }

    readonly TermKind KindOf(int slot) => (TermKind)((_kinds >> (slot * KindBits)) & KindMask);

    /// <summary>The pixel term of <paramref name="slot"/>, without a bounds check.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly float PixelsAt(int slot) =>
        Unsafe.Add(ref Unsafe.As<SlotFloats, float>(ref Unsafe.AsRef(in _pixels)), slot);

    /// <summary>The extra term of <paramref name="slot"/>, without a bounds check.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly float TermAt(int slot) =>
        Unsafe.Add(ref Unsafe.As<SlotFloats, float>(ref Unsafe.AsRef(in _terms)), slot);

    /// <summary>The value in <paramref name="slot"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly Size Read(int slot)
    {
        var kind = KindOf(slot);
        return kind == TermKind.Complex ? ReadComplex(slot) : new Size(PixelsAt(slot), (int)kind, TermAt(slot));
    }

    /// <summary>The multi-term or reference-carrying value in <paramref name="slot"/>.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    readonly Size ReadComplex(int slot) => _complex![slot];

    /// <summary>Stores <paramref name="value"/> in <paramref name="slot"/>, keeping its pixel term inline in every case.</summary>
    void Write(int slot, in Size value)
    {
        var kind = Classify(value, out var term);
        if (kind == TermKind.Complex)
        {
            var copy = new Size[SlotCount];
            _complex?.CopyTo(copy, 0);
            copy[slot] = value;
            _complex = copy;
        }

        _pixels[slot] = value.PixelsContribution;
        _terms[slot] = term;
        var shift = slot * KindBits;
        _kinds = (_kinds & ~(KindMask << shift)) | ((ulong)kind << shift);
    }

    /// <summary>The expand weight in <paramref name="slot"/>, without building the value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly float Expand(int slot)
    {
        var kind = KindOf(slot);
        return kind == TermKind.Expand ? TermAt(slot) : kind == TermKind.Complex ? ReadComplex(slot).ExpandContribution : 0f;
    }

    /// <summary>Which single non-pixel term a value has, or <see cref="TermKind.Complex"/> for several or references.</summary>
    static TermKind Classify(in Size value, out float term)
    {
        term = 0f;
        if (!value.References.IsEmpty)
        {
            return TermKind.Complex;
        }

        var kind = TermKind.None;
        if (!Take(value.PercentageContribution, TermKind.Percentage, ref kind, ref term)
            || !Take(value.RatioContribution, TermKind.Ratio, ref kind, ref term)
            || !Take(value.ExpandContribution, TermKind.Expand, ref kind, ref term)
            || !Take(value.FitContentContribution, TermKind.FitContent, ref kind, ref term)
            || !Take(value.FitLargestContribution, TermKind.FitLargest, ref kind, ref term))
        {
            term = 0f;
            return TermKind.Complex;
        }

        return kind;
    }

    /// <summary>Records a non-zero term; false when one was recorded already, so the value is complex.</summary>
    static bool Take(float contribution, TermKind candidate, ref TermKind kind, ref float term)
    {
        if (IsZeroTerm(contribution))
        {
            return true;
        }

        if (kind != TermKind.None)
        {
            return false;
        }

        kind = candidate;
        term = contribution;
        return true;
    }

    /// <summary>Whether a term is exactly zero (either sign), compared on its bits.</summary>
    static bool IsZeroTerm(float value) => (BitConverter.SingleToInt32Bits(value) & int.MaxValue) == 0;
}
