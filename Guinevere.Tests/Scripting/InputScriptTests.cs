namespace Guinevere.Tests.Scripting;

/// <summary>
/// Covers the JSON input scripts and the player that runs them against a real frame loop.
/// </summary>
public class InputScriptTests
{
    private const string ClickScript = """
        {
          "width": 100,
          "height": 100,
          "steps": [
            { "action": "move", "x": 50, "y": 50 },
            { "action": "click", "button": "left" }
          ]
        }
        """;

    private static (bool Passed, int Failures, List<string> Log) Run(string json, Action<Gui> draw)
    {
        var script = InputScript.FromJson(json);
        Assert.NotNull(script);

        var log = new List<string>();
        var input = new ScriptedInputHandler();
        var player = new InputScriptPlayer(input, log.Add);
        var passed = player.Play(script, new Gui { Input = input }, draw);

        return (passed, player.Failures, log);
    }

    [Fact]
    public void AScriptRoundTripsThroughJson()
    {
        var script = new InputScript
        {
            Width = 800,
            Height = 600,
            Steps =
            [
                new InputScriptStep { Action = InputAction.Move, X = 10, Y = 20 },
                new InputScriptStep { Action = InputAction.Press, Button = MouseButton.Right },
                new InputScriptStep { Action = InputAction.Key, Key = KeyboardKey.Escape },
                new InputScriptStep { Action = InputAction.ExpectCapture, Value = "none" }
            ]
        };

        var restored = InputScript.FromJson(script.ToJson());

        Assert.NotNull(restored);
        Assert.Equal(800, restored.Width);
        Assert.Equal(script.Steps, restored.Steps);
    }

    [Fact]
    public void ActionsAndEnumsAreWrittenAsNames()
    {
        var json = new InputScript
        {
            Steps = [new InputScriptStep { Action = InputAction.ExpectCapture, Button = MouseButton.Middle }]
        }.ToJson();

        Assert.Contains("expectCapture", json, StringComparison.Ordinal);
        Assert.Contains("middle", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedJsonIsRefused()
    {
        Assert.Null(InputScript.FromJson("{ not json"));
    }

    [Fact]
    public void ThePlayerDrivesRealFramesAndTheClickLands()
    {
        var clicks = 0;

        var (passed, failures, _) = Run(ClickScript, gui =>
        {
            using (gui.Node(100, 100, "target").Enter())
                if (gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick())
                    clicks++;
        });

        Assert.True(passed);
        Assert.Equal(0, failures);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ADragHoldsTheCaptureAcrossStepsAndReleasesIt()
    {
        const string json = """
            {
              "width": 100, "height": 100,
              "steps": [
                { "action": "move", "x": 50, "y": 50 },
                { "action": "press" },
                { "action": "move", "x": 80, "y": 50 },
                { "action": "expectCapture", "value": "target" },
                { "action": "release" },
                { "action": "expectCapture", "value": "none" }
              ]
            }
            """;

        var (passed, failures, _) = Run(json, gui =>
        {
            using (gui.Node(100, 100, "target").Enter())
                if (gui.Pass == Pass.Pass2Render)
                    gui.GetInteractable().OnDrag(out _);
        });

        Assert.True(passed);
        Assert.Equal(0, failures);
    }

    [Fact]
    public void AFailedExpectationIsCountedAndReported()
    {
        const string json = """
            { "steps": [ { "action": "expectCapture", "value": "nothing-holds-this" } ] }
            """;

        var (passed, failures, log) = Run(json, gui =>
        {
            using (gui.Node(10, 10, "idle").Enter())
            {
            }
        });

        Assert.False(passed);
        Assert.Equal(1, failures);
        Assert.Contains(log, line => line.Contains("expectCapture", StringComparison.Ordinal));
    }

    [Fact]
    public void NodeExpectationsSeeTheTreeTheFrameBuilt()
    {
        const string json = """
            {
              "steps": [
                { "action": "expectNode", "value": "present" },
                { "action": "expectNoNode", "value": "absent" }
              ]
            }
            """;

        var (passed, failures, _) = Run(json, gui =>
        {
            using (gui.Node(10, 10, "present").Enter())
            {
            }
        });

        Assert.True(passed);
        Assert.Equal(0, failures);
    }

    [Fact]
    public void DumpWritesAPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gv-script-{Guid.NewGuid():N}.png");

        try
        {
            var (passed, _, _) = Run($$"""
                { "width": 40, "height": 40, "steps": [ { "action": "dump", "path": {{System.Text.Json.JsonSerializer.Serialize(path)}} } ] }
                """, gui => gui.DrawRect(gui.ScreenRect, Color.Red));

            Assert.True(passed);
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
