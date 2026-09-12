using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static bool _checkbox1;
    private static bool _checkbox2 = true;
    private static bool _toggle1;
    private static bool _toggle2 = true;
    private static string _textInput = "Hello World";
    private static string _passwordInput = "";
    private static string _numberInput = "12345";
    private static string _rightNumberInput = "42.0";
    private static string _testInput = "";

    private static string _textArea =
        "This is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area\nThis is a\nmultiline\ntext area";

    private static void TextInputsContent(Gui gui)
    {
        // using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
        // {
        //     using (gui.Node(640).Enter())
        //     {
        //         using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Padding(10).Enter())
        //         {
        //             gui.ScrollY(Color.FromArgb(255, 230, 230, 230), Color.FromArgb(255, 200, 200, 200));

        Section(gui, "Single-line Inputs", () => TextInputRow(gui));
        Section(gui, "Text Area", () => TextAreaRow(gui));
        //         }
        //     }

        //     using (gui.Node().Expand().Enter())
        //     {
        //         CurrentValues(gui);
        //     }
        // }
    }

    private static void TextInputRow(Gui gui)
    {
        using (gui.Node().Width(300).Enter())
        {
            _textInput = gui.TextInput(_textInput, placeholder: "Enter text here...");
        }

        using (gui.Node().Width(300).Enter())
        {
            _passwordInput = gui.PasswordInput(_passwordInput, placeholder: "Password");
        }

        using (gui.Node().Width(180).Enter())
        {
            _numberInput = gui.TextInput(_numberInput, placeholder: "Numbers only...");
        }

        using (gui.Node().Width(200).Enter())
        {
            _rightNumberInput = gui.TextInput(_rightNumberInput, placeholder: "123.45", alignX: 1f);
        }

        using (gui.Node().Width(420).Enter())
        {
            _testInput = gui.TextInput(_testInput, placeholder: "Test characters, copy/paste...");
        }
    }

    private static void TextAreaRow(Gui gui)
    {
        using (gui.Node().Height(90).Enter())
        {
            _textArea = gui.TextArea(_textArea, width: 520, height: 90, placeholder: "Enter multiline text...");
        }
    }
}
