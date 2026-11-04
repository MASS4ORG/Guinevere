using SkiaSharp;

namespace Guinevere;

/// <summary>Runs drawing workloads offscreen with the same build, layout and render passes as a window.</summary>
sealed class DrawScene : IDisposable
{
    readonly BenchmarkTarget _target;
    readonly Action<SKCanvas> _frame;
    readonly Action<SKCanvas> _replay;
    Action<Gui> _draw = null!;
    readonly ScriptedInputHandler _input = new();
    readonly Font _font = Font.FromFamilyName("serif");

    /// <summary>Creates the requested target with deterministic input and configured text fonts.</summary>
    public DrawScene(int width, int height, string backend = "raster", bool synchronize = false)
    {
        _target = new BenchmarkTarget(width, height, backend, synchronize);
        _frame = FrameCore;
        _replay = ReplayCore;
        Gui = new Gui { Input = _input, MeasureRenderTimings = true };
        Gui.ConfigureFonts(_font);
    }

    /// <summary>The GUI whose command buffers are measured.</summary>
    public Gui Gui { get; }

    /// <summary>Records and renders one complete frame.</summary>
    public void Frame(Action<Gui> draw)
    {
        _draw = draw;
        _target.Render(_frame);
    }

    void FrameCore(SKCanvas canvas)
    {
        canvas.Clear(SKColors.Transparent);
        Gui.Time.Update(1d / 60);
        Gui.SetStage(Pass.Pass1Build);
        Gui.BeginFrame(canvas);
        _draw(Gui);
        Gui.CalculateLayout();
        Gui.SetStage(Pass.Pass2Render);
        _draw(Gui);
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
        _target.Render(_replay);
    }

    void ReplayCore(SKCanvas canvas)
    {
        canvas.Clear(SKColors.Transparent);
        var restore = canvas.Save();
        Gui.RootNode!.DrawList.Render(Gui, Gui.RootNode, canvas);
        canvas.RestoreToCount(restore);
    }

    /// <summary>Reads a freshly rendered frame for exact same-backend pixel comparisons.</summary>
    public byte[] Pixels(Action<Gui> draw)
    {
        _draw = draw;
        byte[] result = [];
        _target.Render(canvas =>
        {
            FrameCore(canvas);
            var size = canvas.DeviceClipBounds;
            using var bitmap = new SKBitmap(size.Width, size.Height);
            if (canvas.Surface is not { } surface ||
                !surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
            {
                throw new InvalidOperationException("Could not read benchmark pixels.");
            }
            result = bitmap.Bytes;
        });
        return result;
    }

    /// <summary>Writes a workload screenshot when an output directory was requested.</summary>
    public void Dump(string[] args, string name)
    {
        var index = Array.IndexOf(args, "--dump");
        if (index < 0 || index + 1 >= args.Length)
        {
            return;
        }

        Directory.CreateDirectory(args[index + 1]);
        _target.Render(canvas =>
        {
            FrameCore(canvas);
            var size = canvas.DeviceClipBounds;
            using var bitmap = new SKBitmap(size.Width, size.Height);
            if (canvas.Surface is not { } surface ||
                !surface.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0))
            {
                throw new InvalidOperationException("Could not read benchmark pixels.");
            }
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(args[index + 1], name + ".png"));
            data.SaveTo(stream);
        });
    }

    /// <summary>Disposes the offscreen target and the scene's configured font registry.</summary>
    public void Dispose()
    {
        _target.Dispose();
        Gui.Fonts.Dispose();
        _font.Dispose();
    }
}
