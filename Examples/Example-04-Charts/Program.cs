using System.Reflection;
using Guinevere;
using SkiaSharp;

namespace Example04;

/// <summary>Runs the Excalibur charts gallery through the desktop host or the software smoke path.</summary>
public static class Program
{
    /// <summary>Opens the gallery, or renders each chart using a software surface with <c>--headless</c>.</summary>
    public static void Main(string[] args)
    {
        const int Width = 1120;
        const int Height = 760;
        var gui = new Gui();
        ExcaliburStyles.Ensure(gui);
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Example04.Styles.charts.pss")!;
        using var reader = new StreamReader(resource);
        gui.StyleSheets.Add(StyleSheet.Parse(reader.ReadToEnd()));
        var gallery = new ChartsGallery(gui);
        if (args.Contains("--headless", StringComparer.Ordinal))
        {
            gui.Input = new ScriptedInputHandler();
            gui.ConfigureFonts(Font.FromFamilyName("sans-serif", 14));
            using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
            for (var view = 0; view < ChartsGallery.ViewCount; view++)
            {
                gallery.SelectView(view);
                Render($"view {view}");
            }
            gallery.CheckVariants(Render);
            return;

            void Render(string scenario)
            {
                gui.UpdateFrameTime(1d / 60);
                gui.RenderFrame(surface.Canvas, gallery.Draw);
                if (gallery.PlotCommands == 0)
                {
                    throw new InvalidOperationException($"Chart {scenario} produced no draw commands.");
                }
            }
        }
        using var window = new GuiWindow(gui, width: Width, height: Height, title: "Guinevere Charts");
        window.RunGui(gallery.Draw);
    }
}
