using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Scripting;

public class DeviceInputScriptTests
{
    [Fact]
    public void UtilityStepsStillDumpFramesAndReportTrees()
    {
        var input = new ScriptedInputHandler();
        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(100, 100);
        var messages = new List<string>();
        var path = Path.Combine(Path.GetTempPath(), $"guinevere-{Guid.NewGuid():N}.png");
        try
        {
            var script = new InputScript
            {
                Width = 100,
                Height = 100,
                Steps = [new InputScriptStep { Action = InputAction.Tree },
                    new InputScriptStep { Action = InputAction.Dump, Path = path }]
            };
            Assert.True(new InputScriptPlayer(input, messages.Add).Play(script, gui, target =>
            {
                using var node = target.Node(40, 40, "surface").Enter();
                target.DrawBackgroundRect(Color.White);
            }));
            Assert.True(File.Exists(path));
            Assert.Contains(messages, message => message.Contains("surface", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void KeyboardActionsKeepTheirEdgesAndTextAlongsideDeviceReplay()
    {
        var input = new ScriptedInputHandler();
        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(300, 300);
        var frames = new List<(bool Pressed, bool Down, string Text)>();
        var script = InputScript.FromJson("""
            { "steps": [
              { "action": "key", "key": "a" },
              { "action": "hold", "key": "a" },
              { "action": "unhold", "key": "a" },
              { "action": "type", "text": "hello" }
            ] }
            """);
        Assert.NotNull(script);
        Assert.True(new InputScriptPlayer(input).Play(script, gui, target =>
        {
            if (target.Pass == Pass.Pass1Build)
            {
                frames.Add((input.IsKeyPressed(KeyboardKey.A), input.IsKeyDown(KeyboardKey.A), input.GetTypedCharacters()));
            }
        }));
        Assert.Equal([(true, true, ""), (false, false, ""), (true, true, ""), (false, false, ""),
            (false, false, "hello")], frames);
    }

    [Fact]
    public void PinchAndRotateReplayHeadlesslyAlongsideControllerSteps()
    {
        var input = new ScriptedInputHandler();
        var gui = new TestableGui { Input = input, GestureRecognizer = new TouchGestureRecognizer() };
        gui.SetScreenRect(300, 300);
        var gestures = new List<TouchGesture>();
        var samples = new List<GameInputDevice>();
        void Draw(Gui target)
        {
            using (target.Node(300, 300, "surface").Enter())
            {
                target.On<TouchGestureEvent>(e => gestures.Add(e.Gesture));
            }

            if (target.Pass == Pass.Pass1Build)
            {
                samples.AddRange(input.Devices);
            }
        }
        var script = InputScript.FromJson("""
            { "width": 300, "height": 300, "steps": [
              { "action": "frames" },
              { "action": "controllerConnect", "deviceId": "pad" },
              { "action": "controllerButton", "deviceId": "pad", "gameButton": "south", "down": true },
              { "action": "controllerAxis", "deviceId": "pad", "axis": 0, "axisValue": 0.7 },
              { "action": "touch", "contactId": 1, "x": 20, "y": 20, "phase": "began", "pressure": 0.4 },
              { "action": "touch", "contactId": 2, "x": 40, "y": 20, "phase": "began" },
              { "action": "touch", "contactId": 2, "x": 60, "y": 20, "phase": "moved" },
              { "action": "touch", "contactId": 2, "x": 20, "y": 60, "phase": "moved" },
              { "action": "gesture", "contactId": 1, "x": 20, "y": 20, "gestureKind": "swipe" },
              { "action": "controllerDisconnect", "deviceId": "pad" },
              { "action": "reset" }
            ] }
            """);
        Assert.NotNull(script);
        Assert.True(new InputScriptPlayer(input).Play(script, gui, Draw));
        Assert.Contains(samples, sample => sample.Button(GameButton.South).Pressed);
        Assert.Contains(samples, sample => sample.Axes[0] == 0.7f);
        Assert.Contains(gestures, gesture => gesture.Kind == TouchGestureKind.Pinch && gesture.Scale == 2);
        Assert.Contains(gestures, gesture => gesture.Kind == TouchGestureKind.Rotate
            && MathF.Abs(gesture.Rotation - MathF.PI / 2) < 0.001f);
        Assert.Contains(gestures, gesture => gesture.Kind == TouchGestureKind.Swipe);
        Assert.Empty(input.Devices);
        Assert.Null(gui.TouchCapture(1));
    }

    [Fact]
    public void ResetReleasesControllerAndCancelsTouchWithoutStaleEdges()
    {
        var input = new ScriptedInputHandler();
        input.ConnectController("pad");
        input.ConnectController("pad");
        input.SetControllerButton("pad", GameButton.South, true);
        input.NewFrame();
        Assert.False(input.Devices[0].Button(GameButton.South).Pressed);
        Assert.True(input.Devices[0].Button(GameButton.South).HeldSeconds > 0);
        input.SetControllerButton("pad", GameButton.South, false);
        Assert.True(input.Devices[0].Button(GameButton.South).Released);
        input.Touch(new TouchContact(1, Vector2.One, null, TouchPhase.Began));
        input.Reset();
        Assert.Empty(input.Devices);
        Assert.Equal(TouchPhase.Canceled, Assert.Single(input.Contacts).Phase);
        Assert.Equal(new GameDeviceConnection("pad", false), Assert.Single(input.Connections));
        input.NewFrame();
        Assert.Empty(input.Contacts);
    }
}
