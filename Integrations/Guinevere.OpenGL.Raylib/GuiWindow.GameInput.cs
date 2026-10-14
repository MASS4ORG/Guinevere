using System.Diagnostics;
using Raylib_cs;

namespace Guinevere;

public partial class GuiWindow
{
    readonly GameInputState _gameInput = new();
    readonly TouchInputState _touchInput = new();
    readonly Stopwatch _gameInputClock = Stopwatch.StartNew();
    static readonly int[] RaylibButtons = [7, 6, 8, 5, 9, 11, 13, 15, 14, 16, 17, 1, 2, 3, 4];

    void PollGameInput()
    {
        _gameInput.Poll(ReadController, 4, _gameInputClock.Elapsed.TotalSeconds);
        if (_gameInput.Changed)
        {
            Pacing.NotifyInput();
        }

        PollTouchInput();
    }

    GameInputSample? ReadController(int slot)
    {
        if (!Raylib.IsGamepadAvailable(slot))
        {
            return null;
        }

        return new GameInputSample($"raylib:{slot}", Raylib.GetGamepadName_(slot), true,
            ReadControllerButtons(slot), ReadControllerAxes(slot), []);
    }

    static bool[] ReadControllerButtons(int slot) => [.. RaylibButtons.Select(button => (bool)Raylib.IsGamepadButtonDown(slot, (GamepadButton)button))];

    static float[] ReadControllerAxes(int slot) => [.. Enumerable.Range(0, Raylib.GetGamepadAxisCount(slot)).Select(axis => Raylib.GetGamepadAxisMovement(slot, (GamepadAxis)axis))];

    void PollTouchInput()
    {
        var contacts = Enumerable.Range(0, Raylib.GetTouchPointCount()).Select(ReadTouchContact).ToArray();
        _touchInput.SynchronizeContacts(contacts);
        if (_touchInput.Changed)
        {
            Pacing.NotifyInput();
        }
    }

    static TouchContact ReadTouchContact(int index) =>
        new(Raylib.GetTouchPointId(index), Raylib.GetTouchPosition(index), null, TouchPhase.Stationary);
}
