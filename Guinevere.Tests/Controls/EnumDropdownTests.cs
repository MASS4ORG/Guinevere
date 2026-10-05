using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Checks enum bit operations and every enum presentation through scripted frames.</summary>
public sealed class EnumDropdownTests
{
    [Flags]
    enum Flags : ulong { None = 0, A = 1, B = 2, Both = 3, High = 1UL << 63 }
    enum Ordinary { First = 1, Second = 7, Third = 12 }
    [Flags] enum Signed8 : sbyte { High = sbyte.MinValue, All = -1 }
    [Flags] enum Signed16 : short { High = short.MinValue, All = -1 }
    [Flags] enum Signed32 { High = int.MinValue, All = -1 }
    [Flags] enum Signed64 : long { High = long.MinValue, All = -1 }
    [Flags] enum Unsigned8 : byte { High = 128 }
    [Flags] enum Unsigned16 : ushort { High = 32768 }
    [Flags] enum Unsigned32 : uint { High = 1U << 31 }
    [Flags] enum WithoutZero { A = 1, B = 2 }
    enum Empty;

    /// <summary>Zero, composites and bit 63 behave as masks while ordinary enums select exact values.</summary>
    [Fact]
    public void EnumSelectionPreservesUnknownBitsAndHandlesComposites()
    {
        Enum value = Flags.A | Flags.High | (Flags)16;
        Assert.True(EnumSelection.Contains(value, Flags.High));
        Assert.False(EnumSelection.Contains(value, Flags.Both));
        value = EnumSelection.Apply(value, new(Flags.Both, true));
        Assert.True(EnumSelection.Contains(value, Flags.Both));
        value = EnumSelection.Apply(value, new(Flags.Both, false));
        Assert.Equal(Flags.High | (Flags)16, value);
        Assert.Equal(Flags.None, EnumSelection.Apply(value, new(Flags.None, true)));
        Assert.Equal(value, EnumSelection.Apply(value, new(Flags.None, false)));
        Assert.True(EnumSelection.Contains(Flags.None, Flags.None));
        Assert.False(EnumSelection.Contains(value, Flags.None));
        Assert.Equal(Ordinary.Second, EnumSelection.Apply(Ordinary.First, new(Ordinary.Second, true)));
        Assert.Equal(Ordinary.First, EnumSelection.Apply(Ordinary.First, new(Ordinary.Second, false)));
        Assert.True(EnumSelection.Contains(Ordinary.First, Ordinary.First));
        Assert.False(EnumSelection.Contains(Ordinary.First, Ordinary.Second));
        Assert.Throws<ArgumentException>(() => EnumSelection.Contains(Flags.A, Ordinary.First));
        Assert.Throws<ArgumentNullException>(() => EnumSelection.Contains(null!, Flags.A));
        Assert.Throws<ArgumentNullException>(() => EnumSelection.Apply(Flags.A, new(null!, true)));
    }

    /// <summary>Every signed and unsigned enum backing width retains its high bit.</summary>
    [Fact]
    public void EveryBackingWidthSupportsTheHighBit()
    {
        Enum[] high =
        [
            Signed8.High, Signed16.High, Signed32.High, Signed64.High,
            Unsigned8.High, Unsigned16.High, Unsigned32.High, Flags.High,
        ];
        foreach (var option in high)
        {
            var zero = (Enum)Enum.ToObject(option.GetType(), 0);
            var selected = EnumSelection.Apply(zero, new(option, true));
            Assert.Equal(option, selected);
            Assert.True(EnumSelection.Contains(selected, option));
            Assert.Equal(zero, EnumSelection.Apply(selected, new(option, false)));
        }
        Assert.Equal(Signed8.All, EnumSelection.Apply(Signed8.High, new(Signed8.All, true)));
        Assert.Equal(Signed16.All, EnumSelection.Apply(Signed16.High, new(Signed16.All, true)));
        Assert.Equal(Signed32.All, EnumSelection.Apply(Signed32.High, new(Signed32.All, true)));
        Assert.Equal(Signed64.All, EnumSelection.Apply(Signed64.High, new(Signed64.All, true)));
    }

    /// <summary>Flag dropdowns keep combinations selected and stay open while individual bits change.</summary>
    [Fact]
    public void FlagDropdownEditsCompositeAndHighValues()
    {
        using var h = new FrameHarness(500, 500);
        var value = Flags.High;
        var writes = 0;
        void Draw(Gui gui)
        {
            if (gui.EnumDropdown(ref value, width: 200, height: 24, filePath: "flags", lineNumber: 0)) writes++;
        }
        h.Frame(Draw);
        Click(h, Draw, "/button");
        Click(h, Draw, "/option/3");
        Assert.Equal(Flags.Both | Flags.High, value);
        Click(h, Draw, "/option/1");
        Assert.Equal(Flags.B | Flags.High, value);
        Click(h, Draw, "/option/0");
        Assert.Equal(Flags.None, value);
        Assert.Equal(3, writes);
        Assert.NotNull(Find(h, "/popup"));
    }

    /// <summary>Ordinary enums choose one declared value and close their dropdown.</summary>
    [Fact]
    public void OrdinaryDropdownSelectsOneAndReflectsExternalValues()
    {
        using var h = new FrameHarness(500, 500);
        var value = Ordinary.First;
        void Draw(Gui gui) => gui.EnumDropdown(ref value, width: 200, height: 24,
            display: v => $"Choice {v}", filePath: "ordinary", lineNumber: 0);
        h.Frame(Draw);
        Click(h, Draw, "/button");
        Click(h, Draw, "/option/1");
        Assert.Equal(Ordinary.Second, value);
        Assert.Null(Find(h, "/popup"));
        value = Ordinary.Third;
        h.Frame(Draw);
        Click(h, Draw, "/button");
        Click(h, Draw, "/option/0");
        Assert.Equal(Ordinary.First, value);
    }

    /// <summary>Toggle buttons support flags, ordinary choices, keyboard activation and disabled controls.</summary>
    [Fact]
    public void ToggleButtonsUseSameSelectionRules()
    {
        using var h = new FrameHarness(500, 500);
        var value = Flags.A;
        var enabled = true;
        void Draw(Gui gui) => gui.EnumDropdown(ref value, EnumPresentation.ToggleButtons,
            width: 480, height: 24, enabled: enabled, filePath: "toggle", lineNumber: 0);
        h.Frame(Draw);
        Click(h, Draw, "/toggle/B");
        Assert.Equal(Flags.Both, value);
        Click(h, Draw, "/toggle/None");
        Assert.Equal(Flags.None, value);
        h.Gui.RequestFocus(Find(h, "/toggle/High")!.Id);
        h.Frame(Draw);
        h.Input.PressKey(KeyboardKey.Space);
        h.Frame(Draw);
        h.Input.ReleaseKey(KeyboardKey.Space);
        h.Frame(Draw);
        Assert.Equal(Flags.High, value);
        enabled = false;
        h.Frame(Draw);
        Click(h, Draw, "/toggle/A");
        Assert.Equal(Flags.High, value);

        var ordinary = Ordinary.First;
        void DrawOrdinary(Gui gui) => gui.EnumDropdown(ref ordinary, EnumPresentation.ToggleButtons,
            filePath: "plain-buttons", lineNumber: 0);
        h.Frame(DrawOrdinary);
        Click(h, DrawOrdinary, "/toggle/Third");
        Assert.Equal(Ordinary.Third, ordinary);
    }

    /// <summary>Paging wraps, recovers undefined values and retains a searchable dropdown.</summary>
    [Fact]
    public void PagingCyclesDeclaredValuesAndKeepsDropdown()
    {
        using var h = new FrameHarness(500, 500);
        var value = Ordinary.First;
        var enabled = true;
        void Draw(Gui gui) => gui.EnumDropdown(ref value, EnumPresentation.Paging,
            width: 200, height: 24, enabled: enabled, filePath: "paging", lineNumber: 0);
        h.Frame(Draw);
        Click(h, Draw, "/previous");
        Assert.Equal(Ordinary.Third, value);
        Click(h, Draw, "/next");
        Assert.Equal(Ordinary.First, value);
        value = (Ordinary)42;
        h.Frame(Draw);
        Click(h, Draw, "/next");
        Assert.Equal(Ordinary.First, value);
        value = (Ordinary)42;
        h.Frame(Draw);
        Click(h, Draw, "/previous");
        Assert.Equal(Ordinary.Third, value);
        Click(h, Draw, "/center/button");
        Click(h, Draw, "/center/option/1");
        Assert.Equal(Ordinary.Second, value);
        enabled = false;
        h.Frame(Draw);
        Click(h, Draw, "/next");
        Assert.Equal(Ordinary.Second, value);
    }

    /// <summary>An enum without members renders inert paging buttons.</summary>
    [Fact]
    public void EmptyEnumCanRenderEveryPresentation()
    {
        using var h = new FrameHarness();
        var value = (Empty)0;
        foreach (var presentation in Enum.GetValues<EnumPresentation>())
        {
            void Draw(Gui gui) =>
                gui.EnumDropdown(ref value, presentation, filePath: presentation.ToString(), lineNumber: 0);
            h.Frame(Draw);
            if (presentation == EnumPresentation.Paging) Click(h, Draw, "/next");
            if (presentation == EnumPresentation.Dropdown)
            {
                Click(h, Draw, "/button");
                Assert.NotNull(Find(h, "/empty"));
            }
            Assert.Equal((Empty)0, value);
        }
    }

    internal static LayoutNode? Find(FrameHarness harness, string suffix) =>
        MultiDropdownTests.Nodes(harness.Gui.RootNode!)
            .FirstOrDefault(node => node.Id.EndsWith(suffix, StringComparison.Ordinal));

    /// <summary>Flags lacking a declared zero still offer a way to clear every bit.</summary>
    [Fact]
    public void FlagEnumsWithoutZeroOfferNone()
    {
        using var h = new FrameHarness(500, 500);
        var value = WithoutZero.A | WithoutZero.B;
        void Draw(Gui gui) => gui.EnumDropdown(ref value, width: 200, height: 24,
            filePath: "without-zero", lineNumber: 0);
        h.Frame(Draw);
        Click(h, Draw, "/button");
        Click(h, Draw, "/option/0");
        Assert.Equal((WithoutZero)0, value);

        value = WithoutZero.B;
        void DrawPaged(Gui gui) => gui.EnumDropdown(ref value, EnumPresentation.Paging,
            width: 200, height: 24, filePath: "without-zero-paged", lineNumber: 0);
        h.Frame(DrawPaged);
        Click(h, DrawPaged, "/next");
        Assert.Equal(WithoutZero.A, value);
    }

    internal static void Click(FrameHarness harness, Action<Gui> draw, string suffix)
    {
        harness.Click(draw, Find(harness, suffix)!.Rect.Center);
        harness.Frame(draw);
    }
}
