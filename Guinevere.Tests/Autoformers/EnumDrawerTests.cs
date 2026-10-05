using Autoformers;
using Guinevere.Tests.Controls;
using Guinevere.Tests.Mocks;
using MASS4.Attributes;

namespace Guinevere.Tests.Autoformers;

/// <summary>Verifies enum presentation attributes and per-owner flag edits in generated forms.</summary>
public sealed class EnumDrawerTests
{
    [Flags]
    enum Bits { None = 0, A = 1, B = 2, C = 4, Both = 3 }
    enum Mode { First, Second, Third }

    sealed class Model
    {
        public Bits Flags { get; set; }
        [EnumButtons] public Bits Buttons { get; set; }
        [EnumButtons] public Mode ModeButtons { get; set; }
        [EnumPaging] public Mode ModePaging { get; set; }
        [EnumPaging] public Bits FlagsPaging { get; set; }
    }

    static FormField Field(Model model, string name) =>
        FormField.ForMember(typeof(Model).GetProperty(name)!, model);

    /// <summary>Adding a mixed flag preserves each owner's unrelated bits and notifies only changed owners.</summary>
    [Fact]
    public void MixedFlagDropdownPreservesUnrelatedBits()
    {
        using var h = new FrameHarness(600, 500);
        var first = new Model { Flags = Bits.A };
        var second = new Model { Flags = Bits.B | Bits.C };
        var field = FormField.Combine([Field(first, nameof(Model.Flags)), Field(second, nameof(Model.Flags))]);
        void Draw(Gui gui) => gui.FormField(field, "flags");
        h.Frame(Draw);
        EnumDropdownTests.Click(h, Draw, "/button");
        EnumDropdownTests.Click(h, Draw, "/option/1");
        Assert.Equal(Bits.A, first.Flags);
        Assert.Equal(Bits.A | Bits.B | Bits.C, second.Flags);
        Assert.True(field.HasMixedValue);
        EnumDropdownTests.Click(h, Draw, "/option/1");
        Assert.Equal(Bits.None, first.Flags);
        Assert.Equal(Bits.B | Bits.C, second.Flags);
        EnumDropdownTests.Click(h, Draw, "/option/0");
        Assert.Equal(Bits.None, first.Flags);
        Assert.Equal(Bits.None, second.Flags);
        Assert.False(field.HasMixedValue);
    }

    /// <summary>Mixed flag buttons apply inclusion to every owner without overwriting the full value.</summary>
    [Fact]
    public void MixedToggleButtonsPreserveOwnerBits()
    {
        using var h = new FrameHarness(600, 500);
        var first = new Model { Buttons = Bits.A };
        var second = new Model { Buttons = Bits.C };
        var field = FormField.Combine([Field(first, nameof(Model.Buttons)), Field(second, nameof(Model.Buttons))]);
        void Draw(Gui gui) => gui.FormField(field, "buttons");
        h.Frame(Draw);
        EnumDropdownTests.Click(h, Draw, "/toggle/A");
        Assert.Equal(Bits.A, first.Buttons);
        Assert.Equal(Bits.A | Bits.C, second.Buttons);
    }

    /// <summary>Ordinary enum buttons resolve mixed values and paging replaces declared flag values.</summary>
    [Fact]
    public void AttributesChooseOrdinaryButtonsAndPaging()
    {
        using var h = new FrameHarness(600, 500);
        var first = new Model { ModeButtons = Mode.First, FlagsPaging = Bits.A };
        var second = new Model { ModeButtons = Mode.Third, FlagsPaging = Bits.C };
        var buttons = FormField.Combine(
            [Field(first, nameof(Model.ModeButtons)), Field(second, nameof(Model.ModeButtons))]);
        void DrawButtons(Gui gui) => gui.FormField(buttons, "modes");
        h.Frame(DrawButtons);
        EnumDropdownTests.Click(h, DrawButtons, "/toggle/Second");
        Assert.Equal(Mode.Second, first.ModeButtons);
        Assert.Equal(Mode.Second, second.ModeButtons);
        var paging = FormField.Combine(
            [Field(first, nameof(Model.FlagsPaging)), Field(second, nameof(Model.FlagsPaging))]);
        void DrawPaging(Gui gui) => gui.FormField(paging, "paging");
        h.Frame(DrawPaging);
        EnumDropdownTests.Click(h, DrawPaging, "/next");
        Assert.Equal(Bits.B, first.FlagsPaging);
        Assert.Equal(Bits.B, second.FlagsPaging);
    }

    /// <summary>Enum drawers preserve translations and labels, and read-only values remain inert.</summary>
    [Fact]
    public void EnumDrawersSupportTranslationAndReadonlyFields()
    {
        using var h = new FrameHarness(600, 500);
        var model = new Model();
        var field = Field(model, nameof(Model.ModePaging));
        var context = new FormRenderContext { Translate = text => $"Translated {text}" };
        void Draw(Gui gui) => gui.FormField(field, "translated", context);
        h.Frame(Draw);
        EnumDropdownTests.Click(h, Draw, "/center/button");
        EnumDropdownTests.Click(h, Draw, "/center/option/2");
        Assert.Equal(Mode.Third, model.ModePaging);

        var locked = FormField.ForMember(typeof(Model).GetProperty(nameof(Model.Flags))!, model,
            new FormOptions { ReadOnly = true });
        void DrawLocked(Gui gui) => gui.FormField(locked, "locked");
        h.Frame(DrawLocked);
        Assert.Null(EnumDropdownTests.Find(h, "/button"));
    }
}
