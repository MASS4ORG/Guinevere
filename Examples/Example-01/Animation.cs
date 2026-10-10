using System.Numerics;
using Guinevere;

namespace Example_01;

public partial class Shared
{
    static readonly string[] EasingDurations = ["1s", "2s", "4s"];
    static readonly float[] EasingSeconds = [1f, 2f, 4f];
    static int easingDuration = 1;
    static bool blendWide;
    static bool springTarget;

    /// <summary>A layout unit blended between pixels and a percentage, and six easing curves racing on a loop.</summary>
    static void Easings(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(16).Enter())
        {
            LayoutBlend(gui, p);
            EasingRace(gui, p);
        }
    }

    static void LayoutBlend(Gui gui, ColorPalette p)
    {
        var progress = gui.AnimateFloat(blendWide ? 1f : 0f, 0.6f);
        using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Padding(16).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 12);
            using (gui.Node().Direction(Axis.Horizontal).Gap(14).ContentAlignY(0.5f).Enter())
            {
                if (Button(gui, p, blendWide ? "Shrink to 120px" : "Grow to 70%", id: "blend/toggle"))
                    Later(() => blendWide = !blendWide);
                Label(gui, p, "UnitValue.Lerp(Pixels(120), Percentage(0.7), t): layout units animate like numbers");
            }

            var width = UnitValue.Lerp(UnitValue.Pixels(120f), UnitValue.Percentage(0.7f), progress);
            using (gui.Node().Width(width).Height(40).AlignContent(0.5f, 0.5f).Enter())
            {
                gui.DrawBackgroundRect(p.Accent, 8);
                gui.DrawText($"{progress:P0}", 13, p.AccentText);
            }
        }
    }

    static void EasingRace(Gui gui, ColorPalette p)
    {
        Easing.TryParse("ease", out var cssEase);
        (string Name, Func<float, float> Curve)[] curves =
        [
            ("Linear", Easing.Linear), ("EaseInOutSine", Easing.EaseInOutSine), ("CSS ease", cssEase),
            ("SmoothStep", Easing.SmoothStep), ("EaseOutBack", Easing.EaseOutBack),
            ("EaseOutElastic", Easing.EaseOutElastic),
        ];

        var seconds = EasingSeconds[easingDuration];
        var time = gui.Time.Elapsed % seconds / seconds;
        gui.RequestFrame();

        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(16).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 12);
            using (gui.Node().Direction(Axis.Horizontal).Gap(14).ContentAlignY(0.5f).Enter())
            {
                Label(gui, p, "Loop duration");
                Choice(gui, p, "easing-duration", EasingDurations, easingDuration, index => easingDuration = index);
            }

            for (var i = 0; i < curves.Length; i++)
            {
                var (name, curve) = curves[i];
                var eased = curve(time);
                using var key = gui.ItemKey(name);
                using (gui.Node().ExpandWidth().Height(26).Direction(Axis.Horizontal).Gap(14).ContentAlignY(0.5f)
                           .Enter())
                {
                    using (gui.Node(width: 120).Enter()) Label(gui, p, name);
                    using (gui.Node(height: 10).Expand().Enter())
                    {
                        var track = gui.CurrentNode.Rect;
                        gui.DrawBackgroundRect(p.Track, 5);
                        gui.DrawRect(new Rect(track.X, track.Y, Math.Max(2, track.W * Math.Clamp(eased, 0f, 1f)), track.H),
                            new Color(p.Pick(i), 0.45f), 5);
                        gui.DrawCircle(new Vector2(track.X + 8 + (track.W - 16) * eased, track.Y + track.H * 0.5f), 8,
                            p.Pick(i));
                    }
                }
            }
        }
    }

    /// <summary>Keyed springs and tweens chasing one target, a color/pointer follower and spinning transform cards.</summary>
    static void Motion(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(16).Enter())
        {
            Springs(gui, p);
            Cards(gui, p);
        }
    }

    static void Springs(Gui gui, ColorPalette p)
    {
        using (gui.Node().ExpandWidth().Height(250).Direction(Axis.Vertical).Padding(16).Gap(10).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 12);
            using (gui.Node().Direction(Axis.Horizontal).Gap(10).ContentAlignY(0.5f).Enter())
            {
                if (Button(gui, p, "Flip target", id: "spring/flip")) Later(() => springTarget = !springTarget);
                if (Button(gui, p, gui.ReduceMotion ? "Reduce motion: on" : "Reduce motion: off", gui.ReduceMotion,
                        "spring/reduce"))
                    Later(() => gui.ReduceMotion = !gui.ReduceMotion);
            }

            var target = springTarget ? 1f : 0f;
            Follower(gui, p, "Smoothdamp", gui.Smoothdamp(target, 8f), 0);
            Follower(gui, p, "AnimateSpring (bouncy)", gui.AnimateSpring(target, 14f, 0.25f), 1);
            Follower(gui, p, "AnimateFloat (sine)", gui.AnimateFloat(target, 0.6f, Easing.EaseInOutSine), 2);

            var color = gui.SmoothdampColor(springTarget ? p.Warning : p.Accent, 6f);
            var pointer = gui.Smoothdamp(gui.LocalMousePosition, 10f);
            using (gui.Node().Expand().Padding(8).Enter())
            {
                var area = gui.CurrentNode.Rect;
                gui.DrawRect(area, p.Track, 8);
                var dot = Vector2.Clamp(pointer, new Vector2(area.X + 12, area.Y + 12),
                    new Vector2(area.X + area.W - 12, area.Y + area.H - 12));
                gui.DrawCircle(dot, 10, color);
                Label(gui, p, "Smoothdamp(Vector2) and SmoothdampColor follow the pointer");
            }
        }
    }

    static void Follower(Gui gui, ColorPalette p, string label, float value, int index)
    {
        using var key = gui.ItemKey(label);
        using (gui.Node().ExpandWidth().Height(22).Direction(Axis.Horizontal).Gap(12).Enter())
        {
            using (gui.Node(190, 22).Enter()) Label(gui, p, label);
            using (gui.Node().Expand().Enter())
            {
                if (gui.Pass != Pass.Pass2Render) return;
                var bar = gui.CurrentNode.Rect;
                gui.DrawRect(bar, p.Track, 4);
                gui.DrawCircle(new Vector2(bar.X + 9 + (bar.W - 18) * value, bar.Y + bar.H * 0.5f), 8, p.Pick(index));
            }
        }
    }

    /// <summary>Hover grows a card, click spins it; hit testing follows the transformed shape.</summary>
    static void Cards(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(24).Padding(12).ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 12);
            for (var i = 0; i < 5; i++)
            {
                using var key = gui.ItemKey(i);
                SpinCard(gui, p, i);
            }
        }
    }

    static void SpinCard(Gui gui, ColorPalette p, int index)
    {
        // Hover is known once the frame is laid out, so the render pass records it for the next frame's target.
        ref var hovered = ref gui.GetData(false);
        ref var spins = ref gui.GetData(0);
        var scale = gui.AnimateFloat(hovered ? 1.25f : 1f, 0.25f, Easing.EaseOutBack);
        // The idle tilt follows time alone, so the card keeps asking for frames.
        gui.RequestFrame();
        var tilt = hovered ? 0f : MathF.Sin(gui.Time.Elapsed * 1.3f + index) * 0.12f;
        var angle = gui.AnimateSpring(spins * MathF.Tau + tilt, 9f, 0.45f);

        var node = gui.Node(84, 84).AlignContent(0.5f, 0.5f);
        using (node.Enter())
        {
            gui.SetTransform(Matrix3x2.CreateRotation(angle, node.Center) * Matrix3x2.CreateScale(scale, node.Center));
            if (gui.Pass == Pass.Pass2Render)
            {
                hovered = node.OnHover();
                if (node.OnClick()) spins++;
            }

            gui.DrawRect(node.Rect, Color.Lerp(p.Pick(index), p.AccentText, hovered ? 0.2f : 0f), 18);
            gui.DrawText($"Card {index + 1}", 13, p.AccentText);
        }
    }
}
