using System.Numerics;
using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_72_PanGui_AirbnbSlider;

public abstract class Program
{
    private static int _month = 1;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1024, 768, "Airbnb Slider Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawBackgroundRect(Color.White);

        DrawAirBnBMonthSlider(gui, ref _month);
    }

    private static void DrawAirBnBMonthSlider(Gui gui, ref int month)
    {
        float t = month / 12.0f;

        float innerRadius = 90;
        float outerRadius = 150;

        using (gui.Node(500, 500).AlignContent(0.5f).Gap(10).Enter())
        {
            gui.DrawText(month.ToString(), 100f);
            gui.DrawText(month == 1 ? "month" : "months", 20f);

            float halfThickness = (outerRadius - innerRadius) * 0.5f;
            Vector2 handlePos = gui.CurrentNode.Rect.Center +
                                Angle.Turns(t - 0.25f).GetDirectionVector() * (innerRadius + halfThickness);
            Vector2 center = gui.CurrentNode.Rect.Center;

            Shape arcLaneShape = Shape.Circle(outerRadius) - Shape.Circle(innerRadius);
            Shape arcShape = Shape.Arc(innerRadius + halfThickness, halfThickness, Angle.Turns(-0.25f), Angle.Turns(t))
                .Expand(-3);
            Shape handleShape = Shape.Circle(halfThickness - 10);

            InteractableElement handleElement = gui.GetInteractable(handlePos, handleShape);

            if (handleElement.OnHold())
            {
                Vector2 delta = center - gui.Input.MousePosition;
                t = 1 - 0.5f + MathF.Atan2(delta.X, -delta.Y) / MathF.Tau;
                t = Math.Clamp(t, 1 / 12f, 1);
                month = (int)Math.Round(t * 12);
            }
            else
            {
                t = ImMath.Lerp(t, month / 12.0f, gui.Time.DeltaTime * 10);
            }

            gui.DrawShape(center, arcLaneShape)
                .LinearGradientColor(0x00000022, 0x00000005, scale: 0.8f)
                .InnerShadow(0x00000066, new Vector2(0, 20), 50, -30)
                .OuterShadow(0x00000066, new Vector2(0, -5), 10, 5);

            for (int i = 0; i < 12; i++)
                gui.DrawCircle(center + Angle.Turns(i / 12.0f).GetDirectionVector() * (innerRadius + halfThickness), 2,
                    0x00000099);

            // gui.SetClipArea(gui.CurrentNode, arcLaneShape); // Clips the shadow to the lane.

            gui.DrawShape(center, arcShape)
                .RadialGradientColor(0xBA0057FF, 0xF91E50FF, innerRadius, outerRadius)
                .RadialGradientColor(0xDC4682FF, 0xCF2D6C00, innerRadius - halfThickness,
                    innerRadius + halfThickness, offsetY: halfThickness)
                .InnerShadow(0xFA144BFF, new Vector2(0, 5), 25, -8)
                .OuterShadow(0xEA1C5Acc, 90)
                .OuterShadow(0x000000822, new Vector2(0, 3), 10, 3)
                .OuterShadow(0x000000811, 5, 0)
                .OuterShadow(0x22222244, 2);

            gui.DrawShape(handlePos,
                    handleShape.Expand(handleElement.On(Interactions.Hover | Interactions.Hold) ? 4 : 0))
                .SolidColor(0x00000088)
                .LinearGradientColor(0xD4D1D5FF, 0xFFFCFFFF)
                .InnerShadow(0xffffffff, 1, 2)
                .OuterShadow(0x00000066, 4);
        }
    }
}
