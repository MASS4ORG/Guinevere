using System.Numerics;
using Guinevere;

namespace Example_06_Animation;

/// <summary>Keyed springs, tweens and node transforms.</summary>
static class MotionDemos
{
    static readonly Color Panel = Color.FromArgb(52, 58, 64);
    static readonly Color Track = Color.FromArgb(74, 85, 104);
    static readonly Color Accent = Color.FromArgb(74, 144, 226);
    static bool _on;

    /// <summary>Three followers chase the same target: a critically damped spring, a bouncy one and an eased tween.</summary>
    public static void Springs(Gui gui)
    {
        var panel = gui.Node().Expand().Padding(20).Gap(10);
        using (panel.Enter())
        {
            gui.DrawRect(panel.Rect, Panel, 8);
            gui.DrawText("Springs and tweens: click the panel to flip the target", 16, Color.White);
            var overToggle = ReduceMotionToggle(gui);
            if (gui.Pass == Pass.Pass2Render && panel.OnClick() && !overToggle) _on = !_on;

            var target = _on ? 1f : 0f;
            Follower(gui, "Smoothdamp", gui.Smoothdamp(target, 8f));
            Follower(gui, "AnimateSpring (damping 0.25)", gui.AnimateSpring(target, 14f, 0.25f));
            Follower(gui, "AnimateFloat (ease-in-out-sine)", gui.AnimateFloat(target, 0.6f, Easing.EaseInOutSine));

            var color = gui.SmoothdampColor(_on ? Color.FromArgb(255, 240, 120, 80) : Accent, 6f);
            var pointer = gui.Smoothdamp(gui.LocalMousePosition, 10f);
            using (gui.Node().Expand().Enter())
            {
                if (gui.Pass != Pass.Pass2Render) return;
                var area = gui.CurrentNode.Rect;
                gui.DrawRect(area, Track, 6);
                var dot = Vector2.Clamp(pointer, new Vector2(area.X + 12, area.Y + 12),
                    new Vector2(area.X + area.W - 12, area.Y + area.H - 12));
                gui.DrawCircle(dot, 10, color);
                gui.DrawText("SmoothdampColor + Smoothdamp(Vector2) follow the pointer", 12, Color.White);
            }
        }
    }

    static void Follower(Gui gui, string label, float value)
    {
        using (gui.Node().ExpandWidth().Height(22).Direction(Axis.Horizontal).Gap(12).Enter())
        {
            using (gui.Node(220, 22).Enter()) gui.DrawText(label, 12, Color.FromArgb(200, 200, 200));
            using (gui.Node().Expand().Enter())
            {
                if (gui.Pass != Pass.Pass2Render) return;
                var bar = gui.CurrentNode.Rect;
                gui.DrawRect(bar, Track, 4);
                gui.DrawCircle(new Vector2(bar.X + 9 + (bar.W - 18) * value, bar.Y + bar.H * 0.5f), 8, Accent);
            }
        }
    }

    /// <summary>Draws the reduce-motion switch; returns whether the pointer is over it.</summary>
    static bool ReduceMotionToggle(Gui gui)
    {
        var button = gui.Node(200, 26);
        using (button.Enter())
        {
            gui.DrawRect(button.Rect, gui.ReduceMotion ? Accent : Track, 6);
            gui.DrawText($"Reduce motion: {(gui.ReduceMotion ? "on" : "off")}", 12, Color.White);
            if (gui.Pass != Pass.Pass2Render) return false;
            if (button.OnClick()) gui.ReduceMotion = !gui.ReduceMotion;
            return button.OnHover();
        }
    }

    /// <summary>Cards grow on hover and spin on click; hovering and clicking follow the drawn shape.</summary>
    public static void Transforms(Gui gui)
    {
        using (gui.Node().Expand().Padding(20).Gap(16).Enter())
        {
            gui.DrawRect(gui.CurrentNode.Rect, Panel, 8);
            gui.DrawText("Transforms: hover grows a card, click spins it", 16, Color.White);
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(28).ContentAlignX(0.5f).ContentAlignY(0.5f)
                       .Enter())
                for (var i = 0; i < 5; i++)
                {
                    using var key = gui.ItemKey(i);
                    Card(gui, i);
                }
        }
    }

    static void Card(Gui gui, int index)
    {
        // Hover is known once the frame is laid out, so the render pass records it for the next frame's target.
        ref var hovered = ref gui.GetData(false);
        ref var spins = ref gui.GetData(0);
        var scale = gui.AnimateFloat(hovered ? 1.25f : 1f, 0.25f, Easing.EaseOutBack);
        // The idle tilt and hue drift follow time alone, so the card keeps asking for frames.
        gui.RequestFrame();
        var tilt = hovered ? 0f : MathF.Sin(gui.Time.Elapsed * 1.3f + index) * 0.12f;
        var angle = gui.AnimateSpring(spins * MathF.Tau + tilt, 9f, 0.45f);

        var node = gui.Node(90, 90);
        using (node.Enter())
        {
            var center = node.Center;
            gui.SetTransform(Matrix3x2.CreateRotation(angle, center) * Matrix3x2.CreateScale(scale, center));
            if (gui.Pass == Pass.Pass2Render)
            {
                hovered = node.OnHover();
                if (node.OnClick()) spins++;
            }
            var hue = (index * 60 + gui.Time.Elapsed * 20) % 360;
            gui.DrawRect(node.Rect, Program.HsvToColor(hue, hovered ? 0.45f : 0.7f, 0.95f), 18);
            gui.DrawText($"Card {index + 1}", 13, Color.White);
        }
    }
}
