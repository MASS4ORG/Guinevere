# Sample-71-PanGui-HelloTriangle

https://pangui.io/
A classic "Hello Triangle" demonstration, but with a Guinevere twist: an animated, vertex-colored triangle alongside text rendering.

Program.cs
```cs
var gui = new Gui();
var win = new GuiWindow(gui);

win.RunGui(() =>
{
gui.DrawRect(gui.ScreenRect, 0x2A2929FF);
gui.DrawWindowTitlebar();

    using (gui.Node().Expand().Gap(40).Margin(40).AlignContent(0.5f).Enter())
    {
        gui.DrawBackgroundRect(0x00000088, radius: 20);

        float time = gui.Time.Elapsed * 2;
        Vector2 center = gui.Node(200, 200).Rect.Center;
        Vector2 p1 = center + new Vector2(-100 * Cos(time), -100 + Sin(time) * 10);
        Vector2 p2 = center + new Vector2(+100 * Cos(time), -100 - Sin(time) * 10);
        Vector2 p3 = center + new Vector2(0, 100);

        gui.DrawTriangle(p1, p2, p3, Color.Red, Color.Green, Color.Blue);
        gui.DrawText("Hello, Triangle!", color: Color.White, fontSize: 50);
    }
});
```
