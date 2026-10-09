using SkiaSharp;

namespace Guinevere;

/// <summary>Runs drawing workloads offscreen with the same build, layout and render passes as a window.</summary>
sealed class DrawScene : IDisposable
{
    readonly SKSurface _surface;
    readonly ScriptedInputHandler _input = new();
    readonly Font _font = Font.FromFamilyName("serif");

    /// <summary>Creates a raster target with deterministic input and configured text fonts.</summary>
    public DrawScene(int width, int height)
    {
        _surface = SKSurface.Create(new SKImageInfo(width, height));
        Gui = new Gui { Input = _input };
        Gui.ConfigureFonts(_font);
    }

    /// <summary>The GUI whose command buffers are measured.</summary>
    public Gui Gui { get; }

    /// <summary>Records and renders one complete frame.</summary>
    public void Frame(Action<Gui> draw)
    {
        _surface.Canvas.Clear(SKColors.Transparent);
        Gui.Time.Update(1d / 60);
        Gui.SetStage(Pass.Pass1Build);
        Gui.BeginFrame(_surface.Canvas);
        draw(Gui);
        Gui.CalculateLayout();
        Gui.SetStage(Pass.Pass2Render);
        draw(Gui);
        Gui.Render();
        Gui.EndFrame();
        _input.NewFrame();
    }

    /// <summary>Replaces a root-only workload in the existing command buffer.</summary>
    public void Record(Action<Gui> draw)
    {
        Gui.RootNode!.DrawList.Clear();
        draw(Gui);
    }

    /// <summary>Replays recorded drawing onto a freshly cleared target.</summary>
    public void Replay()
    {
        _surface.Canvas.Clear(SKColors.Transparent);
        var restore = _surface.Canvas.Save();
        Gui.RootNode!.DrawList.Render(Gui, Gui.RootNode, _surface.Canvas);
        _surface.Canvas.RestoreToCount(restore);
    }

    /// <summary>Writes a workload screenshot when an output directory was requested.</summary>
    public void Dump(string[] args, string name)
    {
        var index = Array.IndexOf(args, "--dump");
        if (index < 0 || index + 1 >= args.Length) return;
        Directory.CreateDirectory(args[index + 1]);
        using var image = _surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(Path.Combine(args[index + 1], name + ".png"));
        data.SaveTo(stream);
    }

    /// <summary>Disposes the offscreen target and the scene's configured font registry.</summary>
    public void Dispose()
    {
        _surface.Dispose();
        Gui.Fonts.Dispose();
        _font.Dispose();
    }
}
