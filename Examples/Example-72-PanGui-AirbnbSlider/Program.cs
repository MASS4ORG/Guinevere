using System.Numerics;
using Guinevere;

namespace Example_72_PanGui_AirbnbSlider;

/// <summary>Shows a circular month slider with continuous dragging and layered shape effects.</summary>
public abstract class Program
{
    static int _month = 1;

    /// <summary>Runs the Airbnb slider window.</summary>
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1024, 768, "Airbnb Slider Demo");

        win.RunGui(() => Draw(gui));
    }

    static void Draw(Gui gui)
    {
        gui.DrawBackgroundRect(Color.White);

        DrawAirBnBMonthSlider(gui, ref _month);
    }

    /// <summary>Draws the slider and updates the selected month while its handle owns the pointer.</summary>
    public static void DrawAirBnBMonthSlider(Gui gui, ref int month)
    {
        ref var t = ref gui.GetFloat(month / 12.0f);

        float innerRadius = 90;
        float outerRadius = 150;

        using (gui.Node(500, 500).AlignContent(0.5f).Gap(10).Enter())
        {
            // Build-pass nodes have no layout yet; dragging uses the last rendered center.
            ref var center = ref gui.GetValue(Vector2.Zero, "airbnb-slider-center");
            if (gui.Pass == Pass.Pass2Render) center = gui.CurrentNode.Rect.Center;
            float halfThickness = (outerRadius - innerRadius) * 0.5f;
            Vector2 handlePos = center +
                                Angle.Turns(t - 0.25f).GetDirectionVector() * (innerRadius + halfThickness);

            Shape arcLaneShape = Shape.Circle(outerRadius) - Shape.Circle(innerRadius);
            Shape handleShape = Shape.Circle(halfThickness - 10);

            var handleElement = new InteractableElement(Shape.Circle(halfThickness - 10, handlePos), gui,
                $"{gui.CurrentNode.Id}:handle", gui.CurrentNode);
            var held = handleElement.OnHold();

            UpdateMonth(gui, held, center, ref t, ref month);

            var labelFont = gui.Fonts.Resolve("sans-serif", weight: 700);
            gui.DrawText(month.ToString(), 100f, font: labelFont);
            gui.DrawText(month == 1 ? "month" : "months", 20f, font: labelFont);
            handlePos = center + Angle.Turns(t - 0.25f).GetDirectionVector() * (innerRadius + halfThickness);
            Shape arcShape = Shape.Arc(innerRadius + halfThickness, halfThickness, Angle.Turns(-0.25f), Angle.Turns(t))
                .Expand(-3);

            using var lane = gui.Node(500, 500).Absolute(0, 0).Enter();
            gui.DrawShape(center, arcLaneShape)
                .LinearGradientColor(new Color(0x00000022), new Color(0x00000005), angleDeg: 90, scale: 0.8f)
                .InnerShadow(new Color(0x00000066), new Vector2(0, 20), 50, -30)
                .OuterShadow(new Color(0x00000044), new Vector2(0, -3), 6);

            for (int i = 0; i < 12; i++)
                gui.DrawCircle(center + Angle.Turns(i / 12.0f).GetDirectionVector() * (innerRadius + halfThickness), 2,
                    new Color(0x00000099));

            gui.SetClipArea(gui.CurrentNode, arcLaneShape);

            gui.DrawShape(center, arcShape)
                .RadialGradientColor(new Color(0xBA0057FF), new Color(0xF91E50FF), innerRadius, outerRadius)
                .RadialGradientColor(new Color(0xDC4682FF), new Color(0xCF2D6C00), innerRadius - halfThickness,
                    innerRadius + halfThickness, offsetY: halfThickness)
                .InnerShadow(new Color(0xFA144BFF), new Vector2(0, 5), 25, -8)
                .InnerShadow(new Color(0x7E003D66), 2, 1)
                .OuterShadow(new Color(0xEA1C5ACC), 90)
                .OuterShadow(new Color(0x00000822), new Vector2(0, 3), 10, 3)
                .OuterShadow(new Color(0x00000811), 5)
                .OuterShadow(new Color(0x22222244), 2);

            gui.DrawShape(handlePos,
                    handleShape.Expand(held || handleElement.OnHover() ? 4 : 0))
                .LinearGradientColor(new Color(0xD4D1D5FF), new Color(0xFFFCFFFF), angleDeg: 90)
                .InnerShadow(new Color(0xFFFFFFFF), 1, 2)
                .OuterShadow(new Color(0x00000066), 4);
        }
    }

    static void UpdateMonth(Gui gui, bool held, Vector2 center, ref float t, ref int month)
    {
        if (gui.Pass != Pass.Pass1Build) return;
        if (held)
        {
            Vector2 delta = center - gui.LocalMousePosition;
            t = Math.Clamp(0.5f + MathF.Atan2(delta.X, -delta.Y) / MathF.Tau, 1 / 12f, 1);
            month = (int)Math.Round(t * 12);
            return;
        }

        var target = month / 12.0f;
        t = gui.ReduceMotion ? target : ImMath.Lerp(t, target, Math.Clamp(gui.Time.DeltaTime * 10, 0, 1));
        if (MathF.Abs(t - target) > 0.0001f) gui.RequestFrame();
        else t = target;
    }
}
