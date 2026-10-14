using System.Diagnostics;
using System.Numerics;

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
        if (!_glfw.JoystickPresent(slot)) return null;
        var guid = _glfw.GetJoystickGUID(slot);
        var (mapped, buttons, axes) = ReadControllerState(slot);
        return new GameInputSample($"glfw:{slot}:{guid}",
            ReadControllerName(slot), mapped, buttons, axes, ReadControllerHats(slot))
        { MappingId = guid };
    }

    string ReadControllerName(int slot) => _glfw.GetJoystickName(slot) ?? "Joystick";

    unsafe (bool Mapped, bool[] Buttons, float[] Axes) ReadControllerState(int slot)
    {
        if (_glfw.GetGamepadState(slot, out var pad))
            return (true, GameInputState.CopyButtons(new ReadOnlySpan<byte>(pad.Buttons, 15)),
                new ReadOnlySpan<float>(pad.Axes, 6).ToArray());
        return ReadRawController(slot);
    }

    unsafe (bool Mapped, bool[] Buttons, float[] Axes) ReadRawController(int slot)
    {
        var buttons = _glfw.GetJoystickButtons(slot, out var count);
        var held = GameInputState.CopyButtons(new ReadOnlySpan<byte>(buttons, count));
        var axes = _glfw.GetJoystickAxes(slot, out count);
        return (false, held, new ReadOnlySpan<float>(axes, count).ToArray());
    }

    unsafe Vector2[] ReadControllerHats(int slot)
    {
        var hats = _glfw.GetJoystickHats(slot, out var count);
        return GameInputState.CopyHats(new ReadOnlySpan<byte>(hats, count));
    }
}
