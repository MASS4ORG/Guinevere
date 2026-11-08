namespace Guinevere.Tests.Profiling;

public sealed class InputRecordingTests
{
    [Fact]
    public void RoundtripPreservesViewportSimultaneousInputRawDevicesAndTouch()
    {
        var input = new ScriptedInputHandler();
        input.MoveTo(12, 24);
        input.PressKey(KeyboardKey.LeftControl);
        input.PressKey(KeyboardKey.C);
        input.PressButton(MouseButton.Left);
        input.TypeText("Olá 🎮");
        input.Scroll(2);
        input.ConnectController("pad");
        input.SetControllerButton("pad", GameButton.South, true);
        input.SetControllerAxis("pad", 0, .75f);
        input.Touch(new TouchContact(7, new Vector2(40, 50), .5f, TouchPhase.Began));
        input.Gesture(new TouchGesture(TouchGestureKind.Pinch, 7, new Vector2(40, 50), Scale: 1.25f));
        var events = new[] { new GuiInputEvent("move", new Vector2(12, 24)), new GuiInputEvent("text", Text: "Olá 🎮") };
        var frame = InputFrameRecorder.Capture(input, .025, 800, 600, 2, events, input, input);
        input.Reset();
        Assert.Equal(.75f, frame.Devices[0].Axes[0]);
        Assert.Equal(TouchPhase.Began, frame.Contacts[0].Phase);
        using var stream = new MemoryStream();
        new InputRecording([frame]).Write(stream);
        stream.Position = 0;
        var copy = Assert.Single(InputRecording.Read(stream).Frames);
        Assert.Equal(frame.DeltaSeconds, copy.DeltaSeconds);
        Assert.Equal(800, copy.Width);
        Assert.Equal(600, copy.Height);
        Assert.Equal(2, copy.ScaleFactor);
        Assert.Equal(frame.Position, copy.Position);
        Assert.Equal(frame.Text, copy.Text);
        Assert.Equal(frame.Events, copy.Events);
        Assert.Equal(frame.Contacts, copy.Contacts);
        Assert.Equal(frame.Gestures, copy.Gestures);
        var delivery = new RecordedInputHandler(input);
        delivery.SetFrame(copy);
        Assert.True(delivery.IsKeyPressed(KeyboardKey.C));
        Assert.True(delivery.IsKeyDown(KeyboardKey.LeftControl));
        Assert.True(delivery.IsAnyKeyDown);
        Assert.True(delivery.IsKeyUp(KeyboardKey.Z));
        Assert.Equal(copy.Previous, delivery.PrevMousePosition);
        Assert.Equal(copy.Position - copy.Previous, delivery.MouseDelta);
        delivery.SetClipboardText("forwarded");
        Assert.Equal("forwarded", delivery.GetClipboardText());
        Assert.True(delivery.IsMouseButtonPressed(MouseButton.Left));
        Assert.False(delivery.IsMouseButtonUp(MouseButton.Left));
        Assert.Equal("Olá 🎮", delivery.GetTypedCharacters());
        Assert.Equal("", delivery.GetTypedCharacters());
        Assert.Equal(2, delivery.MouseWheelDelta);
        Assert.Single(delivery.Devices);
        Assert.True(copy.RetainedBytes > 0);
    }

    [Fact]
    public void RecordingSchemaIsSeparateFromLegacyInputScriptDefaultsAndEquality()
    {
        var json = """{"width":100,"height":80,"steps":[{"action":"hold","key":"leftShift"},{"action":"key","key":"a"}]}""";
        var script = InputScript.FromJson(json)!;
        var step = new InputScriptStep { Action = InputAction.Hold, Key = KeyboardKey.LeftShift };
        Assert.Equal(step, script.Steps[0]);
        Assert.Equal(script.Steps, InputScript.FromJson(script.ToJson())!.Steps);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Schema\":2,\"Frames\":[]}"));
        Assert.Throws<InvalidDataException>(() => InputRecording.Read(stream));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecordedInputFrame(double.NaN, 10, 10, 1,
            default, default, 0, "", [], [], [], []));
    }

    [Fact]
    public void MissingFramesAndMalformedLegacyStepsAreRejected()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Schema\":1,\"Frames\":[null]}"));
        Assert.Throws<InvalidDataException>(() => InputRecording.Read(stream));
        Assert.Throws<InvalidDataException>(() => InputScriptRecording.Compile(InputScript.FromJson("{\"steps\":null}")!));
        Assert.Throws<InvalidDataException>(() => InputScriptRecording.Compile(InputScript.FromJson("{\"steps\":[null]}")!));
    }

    [Fact]
    public void RecorderDoesNotPollTheUnknownKeyboardSentinel()
    {
        var input = Substitute.For<IInputHandler>();
        input.GetTypedCharacters().Returns("");
        input.IsKeyDown(Arg.Is<KeyboardKey>(static key => (int)key < 0))
            .Returns(static _ => throw new ArgumentOutOfRangeException("key"));
        input.IsKeyPressed(Arg.Is<KeyboardKey>(static key => (int)key < 0))
            .Returns(static _ => throw new ArgumentOutOfRangeException("key"));
        Assert.Empty(InputFrameRecorder.Capture(input, .016, 100, 100).HeldKeys);
    }
}
