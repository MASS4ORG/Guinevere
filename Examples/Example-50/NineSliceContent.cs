using System.Globalization;
using Guinevere;
using SkiaSharp;

namespace Controls01;

public abstract partial class Program
{
    static readonly Lazy<SKImage> SliceTexture = new(CreateSliceTexture);
    static Insets _sliceBorder = new(8);
    static double _sliceWidth = 220;
    static double _sliceHeight = 96;
    static bool _sliceGuides = true;

    /// <summary>Shows source guides, editable pixel borders and live previews at several destination sizes.</summary>
    internal static void NineSliceContent(Gui gui)
    {
        ExcaliburStyles.Ensure(gui);
        gui.DrawBackgroundRect(Token(gui, "base-background"));
        gui.DrawText("9-slice authoring", 24, Token(gui, "text"));
        gui.DrawText("Drag the source guides or edit the pixel insets. Corners keep their size as edges stretch.", 14, Token(gui, "text"));
        using (gui.Node().Direction(Axis.Horizontal).Gap(16).Enter())
        {
            gui.NineSliceEditor(SliceTexture.Value, ref _sliceBorder, id: "slice-editor");
            using (gui.Node().Gap(8).Enter())
            {
                gui.DrawText("Destination width", color: Token(gui, "text"));
                gui.NumberField(ref _sliceWidth, min: 1, max: 400, id: "slice-width");
                gui.DrawText("Destination height", color: Token(gui, "text"));
                gui.NumberField(ref _sliceHeight, min: 1, max: 240, id: "slice-height");
                _sliceGuides = gui.Checkbox(_sliceGuides, "Show destination guides");
                // Destination dimensions stay identical in both passes when a numeric field commits during rendering.
                ref var size = ref gui.GetData((Width: 0f, Height: 0f), id: "slice-preview-size");
                if (gui.Pass == Pass.Pass1Build)
                {
                    (size.Width, size.Height) = ((float)_sliceWidth, (float)_sliceHeight);
                }

                gui.NineSlicePreview(SliceTexture.Value, _sliceBorder, size.Width, size.Height, _sliceGuides,
                    id: "slice-live");
                gui.DrawText("Wide, tall and smaller than the corner sum", 14, Token(gui, "text"));
                using (gui.Node().Direction(Axis.Horizontal).Gap(12).Enter())
                {
                    gui.NineSlicePreview(SliceTexture.Value, _sliceBorder, 180, 40, _sliceGuides, "slice-wide");
                    gui.NineSlicePreview(SliceTexture.Value, _sliceBorder, 40, 120, _sliceGuides, "slice-tall");
                    gui.NineSlicePreview(SliceTexture.Value, _sliceBorder, 12, 12, _sliceGuides, "slice-small");
                }
            }
        }
        var b = _sliceBorder;
        gui.DrawText(string.Create(CultureInfo.InvariantCulture,
            $"background-image = url(panel.png); background-slice = {b.Top:0.##} {b.Right:0.##} {b.Bottom:0.##} {b.Left:0.##};"),
            14, Token(gui, "text"));
        gui.DrawText("PSS values use top, right, bottom, left order. Insets uses left, top, right, bottom.", 12, Token(gui, "text"));
    }

    static SKImage CreateSliceTexture()
    {
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = new SKColor(40, 54, 78) };
        canvas.DrawRoundRect(new SKRect(0, 0, 32, 32), 6, 6, paint);
        paint.Color = new SKColor(115, 170, 235);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 2;
        canvas.DrawRoundRect(new SKRect(2, 2, 30, 30), 5, 5, paint);
        paint.Style = SKPaintStyle.Fill;
        paint.Color = new SKColor(235, 185, 90);
        foreach (var x in new[] { 5, 27 })
        {
            foreach (var y in new[] { 5, 27 })
            {
                canvas.DrawCircle(x, y, 2, paint);
            }
        }

        return SKImage.FromBitmap(bitmap);
    }
}
