namespace Guinevere.Tests.Mocks;

/// <summary>Runs full frames — build, layout, render, draw lists — against a scripted input handler.</summary>
public sealed class FrameHarness : IDisposable
{
    readonly SKSurface _surface;

    public FrameHarness(int width = 400, int height = 300)
    {
        _surface = SKSurface.Create(new SKImageInfo(width, height));
        Gui = new TestableGui { Input = Input };
        Gui.SetScreenRect(width, height);
    }

    public ScriptedInputHandler Input { get; } = new();
    public TestableGui Gui { get; }

    public void Frame(Action<Gui> draw)
    {
        Gui.Time.Update(0.016);
        Gui.SetStage(Pass.Pass1Build);
        Gui.BeginFrame(_surface.Canvas);
        draw(Gui);
        Gui.CalculateLayout();
        Gui.SetStage(Pass.Pass2Render);
        draw(Gui);
        Gui.Render();
        Gui.EndFrame();
        Input.NewFrame();
    }

    /// <summary>Presses and releases <paramref name="button"/> at <paramref name="at"/> over two frames.</summary>
    public void Click(Action<Gui> draw, Vector2 at, MouseButton button = MouseButton.Left)
    {
        Input.MoveTo(at);
        Input.PressButton(button);
        Frame(draw);
        Input.ReleaseButton(button);
        Frame(draw);
    }

    public static Vector2 Center(LayoutNode node) =>
        new(node.Rect.X + node.Rect.W * 0.5f, node.Rect.Y + node.Rect.H * 0.5f);

    public void Dispose() => _surface.Dispose();
}
