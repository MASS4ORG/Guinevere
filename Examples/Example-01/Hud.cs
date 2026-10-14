using Guinevere;
using SkiaSharp;

namespace Example01;

public partial class Shared
{
    static readonly Lazy<SKImage> HudMask = new(CreateHudMask);
    static readonly Lazy<SKImage> HudBarSprite = new(CreateHudBarSprite);

    /// <summary>Shows host-driven health, ability cooldown, cast progress and targeting primitives.</summary>
    internal static void Hud(Gui gui, ColorPalette p)
    {
        ApplyPending(gui);
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
        {
            gui.ScrollY(p.Track, p.Border);
            var state = gui.GetData(new HudDemoState());
            using (gui.Node().Direction(Axis.Horizontal).Gap(8).Enter())
            {
                if (Button(gui, p, "Take damage"))
                {
                    Later(() => state.Health = Math.Max(0, state.Health - 20));
                }
                if (Button(gui, p, "Restore"))
                {
                    Later(() => state.Health = 100);
                }
                if (Button(gui, p, "Animate", state.Animate))
                {
                    Later(() => state.Animate = !state.Animate);
                }
            }
            var health = gui.AnimatedRange(state.Health, duration: 0.15f, lagDelay: 0.35f,
                lagDuration: 0.8f, id: "health");
            var healthColor = gui.AnimateColor(Color.Lerp(Color.Red, Color.Lime, Math.Clamp(state.Health / 25, 0, 1)),
                0.2f, id: "health-color");
            var progress = state.Animate ? (float)(gui.Time.Elapsed % 4) / 4f : 0.65f;
            if (state.Animate)
            {
                gui.RequestFrame();
            }
            gui.DrawText("Image skins: raw health / shield with a held damage trail", 15, p.Text);
            using (gui.Node().ExpandWidth().Height(32).ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
            {
                var skin = new FillOptions { Sprite = HudBarSprite.Value, SpriteBorder = new Insets(6), Maximum = 100 };
                gui.DrawFill(gui.CurrentNode.Rect, health.Value, new FillLayers
                {
                    Background = skin with { Color = p.Track },
                    Ghost = skin with { Color = Color.Yellow },
                    Fill = skin with { Color = healthColor }
                }, health.Ghost);
                gui.DrawFill(gui.CurrentNode.Rect, 20, skin with { Color = new Color(0x4488ffaa) });
                gui.DrawText($"{state.Health:0} / 100 health · 20 shield", 14, p.Text);
            }
            gui.DrawText("Ability cooldown: raw elapsed seconds with an eased normalizer", 15, p.Text);
            using (gui.Node(100, 100).ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
            {
                gui.DrawBackgroundRect(p.Accent, 12);
                gui.DrawFill(gui.CurrentNode.Rect, 4 * progress, new FillOptions
                {
                    Kind = FillKind.Radial,
                    Color = new Color(0x000000b0),
                    Normalizer = static elapsed => MathF.Pow(1 - elapsed / 4, 2)
                });
                gui.DrawFill(gui.CurrentNode.Rect, progress,
                    new FillOptions { Kind = FillKind.Radial, InnerRadius = 0.85f, Color = Color.White });
                gui.DrawText($"{4 * (1 - progress):0.0}s", 16, Color.White);
            }
            gui.DrawText("Cast progress / segmented charge", 15, p.Text);
            using (gui.Node().ExpandWidth().Height(28).ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
            {
                gui.DrawBackgroundRect(p.Track);
                gui.DrawFill(gui.CurrentNode.Rect, progress,
                    new FillOptions { Color = p.Accent, Kind = FillKind.Segmented, Segments = 8, Gap = 4 });
                gui.DrawText($"Casting {progress:P0}", 14, p.Text);
            }
            gui.DrawText("Texture mask: shape and soft alpha edges stay fixed as charge fills", 15, p.Text);
            using (gui.Node(220, 44).Enter())
            {
                gui.DrawFill(gui.CurrentNode.Rect, progress,
                    new FillOptions { Mask = HudMask.Value, Color = p.Accent });
            }
            gui.DrawText("Reticle spread / directional warning", 15, p.Text);
            using (gui.Node().ExpandWidth().Height(140).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 8);
                var center = gui.CurrentNode.Rect.Center;
                gui.DrawReticle(center, p.Text, 4 + progress * 16, dotRadius: 2);
                gui.DrawDirection(center, -45, 55, Color.Red, 14);
            }
        }
    }

    static SKImage CreateHudBarSprite()
    {
        using var surface = SKSurface.Create(new SKImageInfo(24, 20));
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        surface.Canvas.DrawRoundRect(new SKRect(0, 0, 24, 20), 6, 6, paint);
        paint.Color = new SKColor(160, 160, 160);
        surface.Canvas.DrawRect(new SKRect(6, 14, 18, 20), paint);
        return surface.Snapshot();
    }

    static SKImage CreateHudMask()
    {
        using var surface = SKSurface.Create(new SKImageInfo(220, 44));
        surface.Canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var builder = new SKPathBuilder();
        builder.MoveTo(0, 22);
        builder.LineTo(18, 2);
        builder.LineTo(202, 2);
        builder.LineTo(220, 22);
        builder.LineTo(202, 42);
        builder.LineTo(18, 42);
        builder.Close();
        using var path = builder.Detach();
        surface.Canvas.DrawPath(path, paint);
        return surface.Snapshot();
    }

    sealed class HudDemoState
    {
        public float Health { get; set; } = 100;
        public bool Animate { get; set; }
    }
}
