using System.Numerics;
using Guinevere;
using Guinevere.OpenGL.SilkNET;
using SkiaSharp;

namespace Sample_09_TextEffects;

/// <summary>
/// Shows <see cref="TextEffects"/> — outline, drop shadow, inner shadow, gradient fill and a
/// combination — applied to one string, plus a bitmap drawn with <see cref="Gui.DrawImage(SKImage, Rect, Rect?, Color?, float)"/>.
/// </summary>
public abstract class Program
{
    private const string Sample = "Atomic Massa";

    private static readonly Color Ink = Color.FromArgb(255, 245, 247, 250);
    private static readonly Color Label = Color.FromArgb(255, 150, 156, 170);
    private static readonly Color RowBg = Color.FromArgb(255, 40, 44, 56);
    private static readonly Color OutlineInk = Color.FromArgb(255, 16, 20, 30);
    private static readonly Color ShadowInk = Color.FromArgb(190, 0, 0, 0);
    private static readonly Color GradA = Color.FromArgb(255, 70, 130, 245);
    private static readonly Color GradB = Color.FromArgb(255, 235, 80, 130);

    private static SKImage? _checker;
    private static SKImage? _btnNormal, _btnHover, _btnPressed, _btnDisabled;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1000, 900, "Text Effects & Images");
        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 24, 26, 34));
        gui.DrawWindowTitlebar();

        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(28).Gap(16).Enter())
        {
            gui.DrawText("TextEffects", 22, Ink);

            Row(gui, "flat", null);
            Row(gui, "outline", new TextEffects
            {
                Outline = new TextEffects.TextOutline(OutlineInk, 2.5f),
            });
            Row(gui, "drop shadow", new TextEffects
            {
                DropShadow = new TextEffects.TextShadow(ShadowInk, new Vector2(3, 5), 6f),
            });
            Row(gui, "inner shadow", new TextEffects
            {
                Gradient = new TextEffects.TextGradient(GradA, GradA),
                InnerShadow = new TextEffects.TextShadow(Color.FromArgb(220, 0, 0, 0), new Vector2(0, 4), 3f),
            });
            Row(gui, "linear gradient", new TextEffects
            {
                Gradient = new TextEffects.TextGradient(GradA, GradB, 25f),
            });
            Row(gui, "combined", new TextEffects
            {
                Outline = new TextEffects.TextOutline(OutlineInk, 2.5f),
                DropShadow = new TextEffects.TextShadow(ShadowInk, new Vector2(3, 5), 6f),
                Gradient = new TextEffects.TextGradient(GradA, GradB, 25f),
            });

            gui.Node(10, 8);
            gui.DrawText("Gui.DrawImage", 22, Ink);

            using (gui.Node(height: 140).ExpandWidth().Direction(Axis.Horizontal).Gap(24).Padding(12).Enter())
            {
                gui.DrawBackgroundRect(RowBg, radius: 8);
                _checker ??= MakeChecker();
                gui.Image(_checker, 116, 116);
                gui.Image(_checker, 116, 116, tint: GradA);
                gui.Image(_checker, 116, 116, opacity: 0.35f);
            }

            gui.Node(10, 8);
            gui.DrawText("9-slice + ImageButton", 22, Ink);

            _btnNormal ??= Button(new SKColor(46, 92, 170), new SKColor(120, 170, 240));
            _btnHover ??= Button(new SKColor(64, 120, 210), new SKColor(150, 200, 255));
            _btnPressed ??= Button(new SKColor(30, 66, 130), new SKColor(90, 140, 210));
            _btnDisabled ??= Button(new SKColor(60, 64, 74), new SKColor(96, 100, 110));
            var slice = new Insets(12);

            using (gui.Node(height: 150).ExpandWidth().Direction(Axis.Horizontal).Gap(20)
                       .AlignContent(0f, 0.5f).Padding(12).Enter())
            {
                gui.DrawBackgroundRect(RowBg, radius: 8);

                using (gui.Node(60, 60).Enter())
                    gui.DrawImageNineSlice(_btnNormal, gui.CurrentNode.Rect, slice);
                using (gui.Node(230, 56).Enter())
                    gui.DrawImageNineSlice(_btnNormal, gui.CurrentNode.Rect, slice);
                using (gui.Node(60, 120).Enter())
                    gui.DrawImageNineSlice(_btnNormal, gui.CurrentNode.Rect, slice);

                gui.SetTextColor(Ink);
                if (gui.ImageButton(_btnNormal, _btnHover, _btnPressed, text: "LAUNCH",
                        width: 150, height: 56, nineSlice: slice, fontSize: 18))
                {
                    Console.WriteLine("LAUNCH clicked");
                }

                gui.ImageButton(_btnNormal, _btnHover, _btnPressed, _btnDisabled, text: "LOCKED",
                    width: 150, height: 56, nineSlice: slice, enabled: false, fontSize: 18);
            }
        }
    }

    private static SKImage Button(SKColor fill, SKColor edge)
    {
        const int size = 40;
        var bitmap = new SKBitmap(size, size);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var rect = new SKRect(1, 1, size - 1, size - 1);
            using var f = new SKPaint();
            f.Color = fill;
            f.IsAntialias = true;
            using var e = new SKPaint();
            e.Color = edge;
            e.IsAntialias = true;
            e.Style = SKPaintStyle.Stroke;
            e.StrokeWidth = 2;
            canvas.DrawRoundRect(rect, 10, 10, f);
            canvas.DrawRoundRect(rect, 10, 10, e);
        }

        return SKImage.FromBitmap(bitmap);
    }

    private static void Row(Gui gui, string label, TextEffects? effects)
    {
        using (gui.Node(height: 68).ExpandWidth().Direction(Axis.Horizontal).Gap(24).Padding(12, 14)
                   .AlignContent(0f, 0.5f).Enter())
        {
            gui.DrawBackgroundRect(RowBg, radius: 8);

            using (gui.Node(width: 160).Enter())
                gui.DrawText(label, 15, Label);

            gui.DrawText(Sample, 40, Ink, effects: effects);
        }
    }

    private static SKImage MakeChecker()
    {
        const int size = 64;
        var bitmap = new SKBitmap(size, size);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var on = ((x / 8) + (y / 8)) % 2 == 0;
            bitmap.SetPixel(x, y, on ? new SKColor(235, 235, 240, 255) : new SKColor(120, 130, 150, 255));
        }

        return SKImage.FromBitmap(bitmap);
    }
}
