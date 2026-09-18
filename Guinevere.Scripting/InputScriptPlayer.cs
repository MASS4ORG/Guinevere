using SkiaSharp;

namespace Guinevere;

/// <summary>
/// Plays an <see cref="InputScript"/> against a real two-pass frame loop, rendering offscreen. Use it
/// to exercise drags, docking and menus in tests or headless automation.
/// </summary>
/// <param name="input">The device the script drives. Also give it to the <see cref="Gui"/> being played.</param>
/// <param name="onMessage">Where dumps, tree reports and failed expectations are written.</param>
/// <param name="outputDirectory">
/// Where a step's relative <see cref="InputScriptStep.Path"/> is written. Absolute paths are used
/// as-is. Defaults to the current directory.
/// </param>
public sealed class InputScriptPlayer(
    ScriptedInputHandler input,
    Action<string>? onMessage = null,
    string? outputDirectory = null)
{
    readonly Action<string> _log = onMessage ?? Console.Out.WriteLine;

    /// <summary>How many <c>Expect*</c> steps failed during the last <see cref="Play"/>.</summary>
    public int Failures { get; private set; }

    /// <summary>
    /// Runs every step. <paramref name="draw"/> is the frame callback, invoked twice per frame exactly
    /// as a window would. Returns false if any expectation failed.
    /// </summary>
    public bool Play(InputScript script, Gui gui, Action<Gui> draw, Font? font = null)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(draw);

        Failures = 0;
        font ??= Font.FromFamilyName("sans-serif", 14);

        using var surface = SKSurface.Create(
            new SKImageInfo(script.Width, script.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));

        foreach (var step in script.Steps) Execute(step, gui, draw, surface, font);

        return Failures == 0;
    }

    void Execute(InputScriptStep step, Gui gui, Action<Gui> draw, SKSurface surface, Font font)
    {
        switch (step.Action)
        {
            case InputAction.Move:
                input.MoveTo(step.X, step.Y);
                break;

            case InputAction.Press:
                input.PressButton(step.Button);
                break;

            case InputAction.Release:
                input.ReleaseButton(step.Button);
                break;

            case InputAction.Click:
                input.PressButton(step.Button);
                Frame(gui, draw, surface, font);
                input.ReleaseButton(step.Button);
                break;

            case InputAction.Scroll:
                input.Scroll(step.Delta);
                break;

            case InputAction.Key:
                input.PressKey(step.Key);
                Frame(gui, draw, surface, font);
                input.ReleaseKey(step.Key);
                break;

            case InputAction.Hold:
                input.PressKey(step.Key);
                break;

            case InputAction.Unhold:
                input.ReleaseKey(step.Key);
                break;

            case InputAction.Type:
                input.TypeText(step.Text ?? string.Empty);
                break;

            case InputAction.Reset:
                input.Reset();
                break;

            case InputAction.Frames:
                for (var i = 0; i < Math.Max(1, step.Count); i++) Frame(gui, draw, surface, font);
                return;

            case InputAction.Dump:
                Frame(gui, draw, surface, font);
                Save(surface, step.Path);
                return;

            case InputAction.Tree:
                Frame(gui, draw, surface, font);
                ReportTree(gui.RootNode!, 0);
                return;

            case InputAction.ExpectCapture:
                Frame(gui, draw, surface, font);
                ExpectCapture(gui, step.Value);
                return;

            case InputAction.ExpectNode:
            case InputAction.ExpectNoNode:
                Frame(gui, draw, surface, font);
                ExpectNode(gui, step.Value, step.Action == InputAction.ExpectNode);
                return;

            default:
                Fail($"unknown action {step.Action}");
                return;
        }

        Frame(gui, draw, surface, font);
    }

    static void Frame(Gui gui, Action<Gui> draw, SKSurface surface, Font font)
    {
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas, font, font);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
        canvas.Flush();

        // After the frame, so actions queued before the next one survive to be seen by both passes.
        (gui.Input as ScriptedInputHandler)?.NewFrame();
    }

    void ExpectCapture(Gui gui, string? expected)
    {
        var actual = gui.PointerCapture;
        var ok = string.IsNullOrEmpty(expected) || expected.Equals("none", StringComparison.OrdinalIgnoreCase)
            ? actual is null
            : actual is not null && actual.Contains(expected, StringComparison.Ordinal);

        if (ok) _log($"expectCapture '{expected}': ok");
        else Fail($"expectCapture '{expected}': pointer capture was {actual ?? "none"}");
    }

    void ExpectNode(Gui gui, string? idFragment, bool shouldExist)
    {
        if (string.IsNullOrEmpty(idFragment))
        {
            Fail("expectNode needs a value");
            return;
        }

        var found = Exists(gui.RootNode!, idFragment);
        if (found == shouldExist) _log($"{(shouldExist ? "expectNode" : "expectNoNode")} '{idFragment}': ok");
        else Fail($"{(shouldExist ? "expectNode" : "expectNoNode")} '{idFragment}': node was {(found ? "" : "not ")}found");
    }

    static bool Exists(LayoutNode node, string idFragment) =>
        node.Id.Contains(idFragment, StringComparison.Ordinal)
        || node.Children.Any(child => Exists(child, idFragment));

    void Save(SKSurface surface, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Fail("dump needs a path");
            return;
        }

        var target = Path.IsPathRooted(path) || outputDirectory is null
            ? path
            : Path.Combine(outputDirectory, path);

        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(target);
        data.SaveTo(file);
        _log($"wrote frame to {target}");
    }

    void ReportTree(LayoutNode node, int depth)
    {
        var r = node.Rect;
        var id = node.Id.Length > 60 ? "…" + node.Id[^58..] : node.Id;
        _log($"{new string(' ', depth * 2)}[{r.X:0},{r.Y:0} {r.W:0}x{r.H:0}] kids={node.Children.Count} {id}");
        foreach (var child in node.Children) ReportTree(child, depth + 1);
    }

    void Fail(string message)
    {
        Failures++;
        _log(message);
    }
}
