using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Platform;

public class GamepadMappingsTests
{
    [Theory]
    [InlineData(0, -1, 100, 0, false)]
    [InlineData(0, 1, 100, 200, false)]
    [InlineData(-1, 0, 0, 100, false)]
    [InlineData(1, 0, 200, 100, false)]
    [InlineData(0, -1, 100, 0, true)]
    [InlineData(0, 1, 100, 200, true)]
    [InlineData(-1, 0, 0, 100, true)]
    [InlineData(1, 0, 200, 100, true)]
    public void UnmappedXboxNavigatesAndActivatesThroughBackendPolling(float x, float y,
        float targetX, float targetY, bool stick)
    {
        using var harness = new FrameHarness();
        var game = new GameInputState();
        var raw = XboxSample();
        harness.Gui.Platform.Register<IGameInputCapability>(game);
        var activated = 0;
        void Draw(Gui gui)
        {
            using (gui.Node(40, 40, "origin").Absolute(100, 100).Enter()) gui.Pressable();
            using (gui.Node(40, 40, "target").Absolute(targetX, targetY).Enter())
                if (gui.Pressable().Activated) activated++;
        }
        void Frame()
        {
            game.Poll(slot => slot == 0 ? raw : null, 16, harness.Gui.Clock.Elapsed);
            harness.Frame(Draw);
            game.NewFrame();
        }
        Frame();
        Assert.True(Assert.Single(game.Devices).IsMapped);
        harness.Gui.RequestFocus("origin");
        Frame();
        Assert.Equal("origin", harness.Gui.Focus.CurrentFocusedId);
        if (stick)
        {
            raw.Axes[0] = x;
            raw.Axes[1] = y;
        }
        else raw.Hats[0] = new Vector2(x, y);
        Frame();
        raw.Axes[0] = raw.Axes[1] = 0;
        raw.Hats[0] = Vector2.Zero;
        Frame();
        Assert.Equal("target", harness.Gui.Focus.CurrentFocusedId);
        raw.Buttons[0] = true;
        Frame();
        raw.Buttons[0] = false;
        Frame();
        Assert.Equal(1, activated);
    }

    [Fact]
    public void FallbackNormalizesAxisOrderAndPreservesUnknownOrAlreadyMappedDevices()
    {
        var raw = XboxSample();
        raw.Axes[3] = 0.6f;
        raw.Axes[4] = -0.7f;
        var mapped = GamepadMappings.Normalize(raw);
        Assert.Equal([-0.048f, 0.024f, 0.6f, -0.7f, -1, -1], mapped.Axes);
        var unknown = raw with { MappingId = null };
        Assert.Same(unknown, GamepadMappings.Normalize(unknown));
        Assert.Same(mapped, GamepadMappings.Normalize(mapped));
    }

    [Theory]
    [InlineData(10, 6)]
    [InlineData(11, 5)]
    public void IncompleteRawDevicesRemainUnmapped(int buttons, int axes)
    {
        var raw = XboxSample() with { Buttons = new bool[buttons], Axes = new float[axes] };
        Assert.Same(raw, GamepadMappings.Normalize(raw));
    }

    static GameInputSample XboxSample() => new("xbox", "Microsoft Xbox Series S|X Controller", false,
        new bool[11], [-0.048f, 0.024f, -1, 0.05f, -0.021f, -1], [Vector2.Zero])
    { MappingId = "030000005e040000120b00000f050000" };
}
