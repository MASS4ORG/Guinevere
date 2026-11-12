namespace Guinevere.Tests.Profiling;

sealed class ProfilerFixture : IDisposable
{
    internal ProfilerFixture(int width = 480, int height = 320)
    {
        Surface = SKSurface.Create(new SKImageInfo(width, height));
        Font = Font.FromFamilyName("sans-serif", 14);
        Gui = new Gui { Input = Input };
        Gui.ConfigureFonts(Font);
    }
    internal ScriptedInputHandler Input { get; } = new();
    internal Gui Gui { get; }
    internal SKSurface Surface { get; }
    internal Font Font { get; }
    internal void Frame(Action draw, double delta = .016)
    {
        Gui.UpdateFrameTime(delta);
        Gui.RenderFrame(Surface.Canvas, draw);
        Input.NewFrame();
    }
    public void Dispose()
    {
        Surface.Dispose();
        Font.Dispose();
    }
}
