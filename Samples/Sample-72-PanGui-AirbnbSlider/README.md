# Sample-72-PanGui-AirbnbSlider

This sample demonstrates advanced shape composition, custom interactions, and high-fidelity visual effects in Guinevere, inspired by the Airbnb mobile app slider.

## Features
- **Complex Shapes**: Creating a circular lane and an arc handle using SkiaSharp-powered CSG (Constructive Solid Geometry) operations.
- **Custom Interaction**: A polar-coordinate based slider handle that can be dragged in a circular motion.
- **Advanced Rendering**:
    - Multi-layered linear and radial gradients.
    - Inner and outer shadows with custom offsets and blurs.
    - Dynamic scaling of the handle on hover/hold.
- **State Interpolation**: Smooth animation of the slider position when not being dragged.

## Interaction
- Drag the circular handle along the arc to select the number of months.
- Watch the smooth transition as you release the mouse button.

## Original Code

Code from https://pangui.io/

Program.cs
```cs
void DrawAirBnBMonthSlider(ref int month)
{
    ref float t = ref gui.GetFloat(month / 12.0f);

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

        SdShape arcLaneShape = SdShape.Circle(outerRadius) - SdShape.Circle(innerRadius);
        SdShape arcShape = SdShape.Arc(innerRadius + halfThickness, halfThickness, Angle.Turns(-0.25f), Angle.Turns(t))
            .Expand(-3);
        SdShape handleShape = SdShape.Circle(halfThickness - 10);

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

        gui.DrawSdShape(center, arcLaneShape)
            .LinearGradientColor(0x00000022, 0x00000005, scale: 0.8f)
            .InnerShadow(0x00000066, new Vector2(0, 20), 50, -30)
            .OuterShadow(0x00000066, new Vector2(0, -5), 10, 5);

        for (int i = 0; i < 12; i++)
            gui.DrawCircle(center + Angle.Turns(i / 12.0f).GetDirectionVector() * (innerRadius + halfThickness), 2,
                0x00000099);

        gui.SetClipArea(gui.CurrentNode, arcLaneShape); // Clips the shadow to the lane.

        gui.DrawSdShape(center, arcShape)
            .RadialGradientColor(0xBA0057FF, 0xF91E50FF, innerRadius, outerRadius)
            .RadialGradientColor(0xDC4682FF, 0xCF2D6C00, innerRadius - halfThickness, innerRadius + halfThickness,
                offsetY: halfThickness)
            .InnerShadow(0xFA144BFF, new Vector2(0, 5), 25, -8)
            .OuterShadow(0xEA1C5Acc, 90)
            .OuterShadow(0x000000822, new Vector2(0, 3), 10, 3)
            .OuterShadow(0x000000811, 5, 0)
            .OuterShadow(0x22222244, 2);

        gui.DrawSdShape(handlePos, handleShape.Expand(handleElement.On(Interactions.Hover | Interactions.Hold) ? 4 : 0))
            .LinearGradientColor(0xD4D1D5FF, 0xFFFCFFFF)
            .InnerShadow(0xffffffff, 1, 2)
            .OuterShadow(0x00000066, 4);
    }
}
```
