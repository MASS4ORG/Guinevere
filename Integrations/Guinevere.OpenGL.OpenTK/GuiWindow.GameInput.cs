using System.Diagnostics;
using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Guinevere;

public partial class GuiWindow
{
    readonly GameInputState _gameInput = new();
    readonly Stopwatch _gameInputClock = Stopwatch.StartNew();

    void PollGameInput()
    {
        _gameInput.Poll(ReadController, 16, _gameInputClock.Elapsed.TotalSeconds);
        if (_gameInput.Changed) Pacing.NotifyInput();
    }

    GameInputSample? ReadController(int slot)
    {
        if (!GLFW.JoystickPresent(slot)) return null;
        var guid = GLFW.GetJoystickGUID(slot);
        var (mapped, buttons, axes) = ReadControllerState(slot);
        return new GameInputSample($"glfw:{slot}:{guid}",
            ReadControllerName(slot), mapped, buttons, axes, ReadControllerHats(slot))
        { MappingId = guid };
    }

    static string ReadControllerName(int slot) => GLFW.GetJoystickName(slot) ?? "Joystick";

    unsafe (bool Mapped, bool[] Buttons, float[] Axes) ReadControllerState(int slot)
    {
        if (GLFW.GetGamepadState(slot, out var pad))
            return (true, GameInputState.CopyButtons(new ReadOnlySpan<byte>(pad.Buttons, 15)),
                new ReadOnlySpan<float>(pad.Axes, 6).ToArray());
        return ReadRawController(slot);
    }

    (bool Mapped, bool[] Buttons, float[] Axes) ReadRawController(int slot) =>
        (false, GameInputState.CopyButtons(GLFW.GetJoystickButtons(slot).ToArray()
            .Select(button => (byte)button).ToArray()), GLFW.GetJoystickAxes(slot).ToArray());

    Vector2[] ReadControllerHats(int slot) => GameInputState.CopyHats(GLFW.GetJoystickHats(slot).ToArray()
        .Select(hat => (byte)hat).ToArray());
}
