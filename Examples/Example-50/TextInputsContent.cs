using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static bool _checkbox1;
    static bool _checkbox2 = true;
    static bool _toggle1;
    static bool _toggle2 = true;
    static string _textInput = "Hello World";
    static string _passwordInput = "monkey123";
    static string _rightAlignmentInput = "Right";
    static float _numberField = 42.5f;
    static string _textArea =
        "This is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area";

    static void TextInputsContent(Gui gui)
    {
        using (gui.Node().Expand().Padding(5).Enter())
        {
            gui.ScrollY();
            Section(gui, "Single-line Inputs", () => TextInputRow(gui));
            Section(gui, "Numbers", () => NumberControlsRow(gui));
            Section(gui, "Text Area", () => TextAreaRow(gui));
        }
    }

    static void TextInputRow(Gui gui)
    {
        gui.SetTextColor(Token(gui, "text"));
        using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            gui.DrawText("Regular Text").Width(150);
            _textInput = gui.TextInput(_textInput, placeholder: "Enter text here...");
        }

        using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            gui.DrawText("Password").Width(150);
            _passwordInput = gui.PasswordInput(_passwordInput, placeholder: "Password");
            gui.DrawText(_passwordInput).Width(150);
        }

        using (gui.Node().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            gui.DrawText("Alignment").Width(150);
            _rightAlignmentInput = gui.TextInput(_rightAlignmentInput, placeholder: "Right", alignX: 1f);
        }

        gui.TextInput(_textInput, placeholder: "Disabled/Read only", enabled: false);
    }

    static void NumberControlsRow(Gui gui)
    {
        gui.SetTextColor(Token(gui, "text"));
        gui.NumberField(ref _numberField, step: 0.25f, min: 0f, max: 100f, alignX: 1f);
        gui.Slider(ref _numberField, step: 0.25f, min: 0f, max: 100f, showValue: true);
        gui.Slider(ref _numberField, step: 0.25f, min: 0f, max: 100f);
        gui.DrawText("Share value", size: 12).ExpandWidth();
    }

    static void TextAreaRow(Gui gui)
    {
        _textArea = gui.TextArea(_textArea, height: 90, placeholder: "Enter multiline text...");
    }

    static readonly Color DisabledInputFill = Color.FromArgb(255, 245, 246, 247);
    static readonly Color DisabledInputInk = Color.FromArgb(255, 160, 162, 167);
}
