using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static bool _checkbox1;
    private static bool _checkbox2 = true;
    private static bool _toggle1;
    private static bool _toggle2 = true;
    private static string _textInput = "Hello World";
    private static string _passwordInput = "monkey";
    private static string _rightAlignmentInput = "Right";

    private static string _textArea =
        "This is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area";

    private static float _sliderValue = 0.5f;
    private static float _sliderStepped = 3f;
    private static float _numberField = 42.5f;

    private static void TextInputsContent(Gui gui)
    {
        Section(gui, "Single-line Inputs", () => TextInputRow(gui));
        Section(gui, "Numbers", () => NumberControlsRow(gui));
        Section(gui, "Text Area", () => TextAreaRow(gui));
    }

    private static void TextInputRow(Gui gui)
    {
        using (gui.Node().Direction(Axis.Horizontal).Enter())
        {
            gui.DrawText("Regular Text").Width(150);
            _textInput = gui.TextInput(_textInput, placeholder: "Enter text here...");
        }

        using (gui.Node().Direction(Axis.Horizontal).Enter())
        {
            gui.DrawText("Password").Width(150);
            _passwordInput = gui.PasswordInput(_passwordInput, placeholder: "Password");
            gui.DrawText(_passwordInput).Width(150);
        }

        using (gui.Node().Direction(Axis.Horizontal).Enter())
        {
            gui.SetTextColor(Color.Red);
            gui.DrawText("Alignment").Width(150);
            _rightAlignmentInput = gui.TextInput(_rightAlignmentInput, placeholder: "Right", alignX: 1f);
        }

        using (gui.Node().Direction(Axis.Horizontal).Enter())
        {
            gui.DrawText("Disabled").Width(150);
            gui.TextInput(_textInput, placeholder: "Read only", enabled: false,
                backgroundColor: DisabledInputFill, textColor: DisabledInputInk,
                placeholderColor: DisabledInputInk);
        }
    }

    private static void TextAreaRow(Gui gui)
    {
        _textArea = gui.TextArea(_textArea, width: 520, height: 90, placeholder: "Enter multiline text...");
    }

    private static void NumberControlsRow(Gui gui)
    {
        gui.Slider(ref _sliderValue, 0f, 1f, showValue: true);
        gui.Slider(ref _sliderStepped, 0f, 10f, step: 0.5f, showValue: true);

        using (gui.Node().Direction(Axis.Horizontal).Enter())
        {
            gui.NumberField(ref _numberField, step: 0.25f, min: 0f, max: 100f);
            gui.Slider(ref _numberField, step: 0.25f, min: 0f, max: 100f, showValue: true);
            gui.DrawText("Drag right/up to increase, left/down to decrease. Click to type.", size: 12);
        }
    }

    private static readonly Color DisabledInputFill = Color.FromArgb(255, 245, 246, 247);
    private static readonly Color DisabledInputInk = Color.FromArgb(255, 160, 162, 167);
}
