using Example02;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Graphs;

static class GraphPlaygroundInput
{
    public static void Command(FrameHarness harness, GraphPlayground playground, string id)
    {
        var commands = playground.Scenario switch
        {
            0 => playground.Sandbox.Commands(harness.Gui),
            1 => playground.Flow.Sandbox.Commands(harness.Gui),
            2 => playground.Fsm.Commands(harness.Gui),
            _ => playground.Groups.Sandbox.Commands(harness.Gui)
        };
        var command = commands.Single(command => command.Id == id);
        var menuIndex = commands.Select(static command => command.Menu).Distinct().ToList().IndexOf(command.Menu);
        var rowIndex = commands.Where(candidate => candidate.Menu == command.Menu).ToList().IndexOf(command);
        Menu(harness, playground, menuIndex, rowIndex);
    }

    public static void Menu(FrameHarness harness, GraphPlayground playground, int menuIndex, int rowIndex)
    {
        OpenMenu(harness, playground, menuIndex, rowIndex);
        GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Enter);
        harness.Frame(playground.Draw);
    }

    public static void Submenu(FrameHarness harness, GraphPlayground playground, int menuIndex, int rowIndex, int childIndex)
    {
        OpenMenu(harness, playground, menuIndex, rowIndex);
        GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Right);
        for (var row = 0; row < childIndex; row++)
        {
            GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Down);
        }
        GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Enter);
        harness.Frame(playground.Draw);
    }

    static void OpenMenu(FrameHarness harness, GraphPlayground playground, int menuIndex, int rowIndex)
    {
        var bar = harness.Gui.RootNode!.FindChildById("playground-main-menu")!;
        harness.Gui.Focus.RequestFocus(bar.ChildNodes[menuIndex].Id);
        harness.Frame(playground.Draw);
        GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Enter);
        harness.Frame(playground.Draw);
        for (var row = 0; row <= rowIndex; row++)
        {
            GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Down);
        }
    }
}
