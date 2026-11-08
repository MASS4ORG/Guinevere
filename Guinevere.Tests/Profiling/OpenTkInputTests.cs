extern alias OpenTk;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTK.Windowing.Common;
using Window = OpenTk::Guinevere.GuiWindow;

namespace Guinevere.Tests.Profiling;

public sealed class OpenTkInputTests
{
    [Fact]
    public void TextJournalFailureKeepsTheNativeInputHandlerUsable()
    {
        var gui = new Gui();
        var window = (Window)RuntimeHelpers.GetUninitializedObject(typeof(Window));
        var characters = new StringBuilder();
        typeof(Window).GetField("_gui", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, gui);
        typeof(Window).GetField("_typedCharacters", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, characters);
        var callback = typeof(Window).GetMethod("OnTextInput", BindingFlags.Instance | BindingFlags.NonPublic)!;
        callback.Invoke(window, [new TextInputEventArgs('A')]);
        Assert.Equal("A", characters.ToString());
        var controller = Substitute.For<IGuiFrameController>();
        controller.When(static target => target.Input(Arg.Any<GuiInputEvent>()))
            .Do(static _ => throw new InvalidOperationException("Rejected host journal"));
        gui.FrameController = controller;
        callback.Invoke(window, [new TextInputEventArgs('B')]);
        Assert.Equal("A", characters.ToString());
        gui.FrameController = null;
        callback.Invoke(window, [new TextInputEventArgs('C')]);
        Assert.Equal("AC", characters.ToString());
    }
}
