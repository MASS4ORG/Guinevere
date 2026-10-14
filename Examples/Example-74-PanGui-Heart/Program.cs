using System.Numerics;
using Guinevere;

namespace Example74PanGuiHeart;

/// <summary>Demonstrates shape operations through an animated, shaded heart.</summary>
public abstract class Program
{
    const float StageDuration = 3.5f;
    const float TransitionDuration = 0.65f;
    static readonly Shape A = Shape.Circle(100).MoveX(-60);
    static readonly Shape B = Shape.Circle(100).MoveX(+60);
    static readonly Shape Tri = Shape.Triangle(-100, 0, 100, 0, 0, 200);
    static readonly Shape Heart = Tri.Union(A + B, 100);
    static readonly (Shape Shape, string Text)[] Stages =
    [
        (Shape.Circle(100), "a = Shape.Circle(100)"),
        (A, "a = Shape.Circle(100).MoveX(-60)"),
        (B, "b = Shape.Circle(100).MoveX(+60)"),
        (A + B, "a + b"),
        (A * B, "a * b"),
        ((A + B).Onion(10), "(a + b).Onion(10)"),
        ((A + B) - (A * B), "(a + b) - (a * b)"),
        (A.MoveX(-85).Union(B.MoveX(85), 100), "a.MoveX(-85).Union(b.MoveX(85), 100)"),
        (A.Union(B, 100), "a.Union(b, 100)"),
        (A.Union(B, 100) - (A - 20), "a.Union(b, 100) - (a - 20)"),
        (A.Union(B, 100) - (B - 20), "a.Union(b, 100) - (b - 20)"),
        (Tri, "tri = Shape.Triangle(-100, 0, 100, 0, 0, 200)"),
        (Tri + A + B, "tri + a + b"),
        (Heart, "heart = tri.Union(a + b, 100)"),
        (Heart, "heart.Scale(1 + Sin(t + PI) * 0.2f)"),
        (Heart, "heart.Mix(b.Rotate(gui.Time.Elapsed * 4), 0.1f)")
    ];

    /// <summary>Opens the animated shape demonstration.</summary>
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);
        win.RunGui(() => Draw(gui));
    }

    /// <summary>Draws one frame, sharing the blended geometry between the build and render passes.</summary>
    public static void Draw(Gui gui)
    {
        gui.DrawBackgroundRect(new Color(0x111111FF));
        gui.SetTextColor(Color.White);
        gui.RequestFrame();
        using var root = gui.Node().Expand().AlignContent(0.5f).Enter();
        var frame = gui.UpdateData<(Shape Shape, string Text)>((null!, ""),
            _ => CreateFrame(gui.Time.Elapsed, gui.ReduceMotion), "heart-frame");
        var center = gui.ScreenRect.Center - new Vector2(0, 60);
        var hit = new InteractableElement(new Rect(center.X - 160, center.Y - 120, 320, 340),
            gui, "heart-hover", gui.CurrentNode);
        var hovered = hit.OnHover();
        var scale = gui.Smoothdamp(hovered ? 1.2f : 1f);
        var stage = (int)(gui.Time.Elapsed / StageDuration) % Stages.Length;
        var red = stage >= 13;
        var fill = red ? new Color(0xE00000FF) : new Color(0x164CF5FF);
        var light = red ? new Color(0xFF6880FF) : new Color(0x74C0FFFF);
        var dark = red ? new Color(0x460000CC) : new Color(0x001347CC);

        gui.DrawShape(center, frame.Shape.Scale(scale))
            .SolidColor(fill)
            .LinearGradientColor(red ? new Color(0xFF1038FF) : new Color(0x367BFFFF),
                red ? new Color(0x9B0000FF) : new Color(0x1230A0FF), 45, 1.3f)
            .InnerShadow(light, new Vector2(9, 9), 10, 0)
            .InnerShadow(dark, new Vector2(-8, -8), 16, 0)
            .OuterShadow(new Color(0x000000CC), new Vector2(0, 3), 4, 2);

        gui.DrawText(frame.Text, 20).Width(700).Absolute(center.X - 350, center.Y + 245);
    }

    /// <summary>Returns an independent shape for a stage without mutating the reusable source shapes.</summary>
    public static Shape CreateStage(int stage, float elapsed)
    {
        stage = Math.Clamp(stage, 0, Stages.Length - 1);
        if (stage == 14)
        {
            return Heart.Scale(1 + MathF.Sin(elapsed + MathF.PI) * 0.2f);
        }

        if (stage == 15)
        {
            return Heart.Mix(B.Copy().Rotate(elapsed * 4), 0.1f);
        }

        return Stages[stage].Shape.Copy();
    }

    /// <summary>Blends successive stages briefly, then holds each operation so its result can be inspected.</summary>
    public static (Shape Shape, string Text) CreateFrame(float elapsed, bool reduceMotion = false)
    {
        var stage = (int)(elapsed / StageDuration) % Stages.Length;
        var target = CreateStage(stage, elapsed);
        var phase = elapsed % StageDuration;
        if (reduceMotion || elapsed < StageDuration || phase >= TransitionDuration)
        {
            return (target, Stages[stage].Text);
        }

        var previous = (stage + Stages.Length - 1) % Stages.Length;
        var t = phase / TransitionDuration;
        t = t * t * (3 - 2 * t);
        return (CreateStage(previous, elapsed).Mix(target, t), Stages[stage].Text);
    }
}
