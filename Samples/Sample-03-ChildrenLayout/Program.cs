using Guinevere;
using Guinevere.OpenGL.SilkNET;
using Axis = Guinevere.Axis;

namespace Sample_03_ChildrenLayout;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 800, 800, "Children Layout Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.Black);

        // Test AlignContent with separate horizontal/vertical values
        using (gui.Node().Expand().Margin(10).Direction(Axis.Vertical).Gap(20).Enter())
        {
            // Title
            using (gui.Node().Height(30).Enter())
            {
                gui.DrawText("AlignContent Test - Separate Horizontal & Vertical Control", color: Color.White, size: 18);
            }

            // Test single value (affects both axes)
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
            {
                CreateLayout(gui, 0.0f, 0.0f, "Vertical: AlignContent(0.0)", Color.LightBlue, Axis.Vertical);
                CreateLayout(gui, 0.5f, 0.5f, "Vertical: AlignContent(0.5)", Color.LightGreen, Axis.Vertical);
                CreateLayout(gui, 1.0f, 1.0f, "Vertical: AlignContent(1.0)", Color.LightCoral, Axis.Vertical);
            }

            // Test separate horizontal/vertical values
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
            {
                CreateLayout(gui, 0.0f, 1.0f, "Vertical: H=0.0, V=1.0", Color.LightSalmon, Axis.Vertical);
                CreateLayout(gui, 1.0f, 0.0f, "Vertical: H=1.0, V=0.0", Color.LightSteelBlue, Axis.Vertical);
                CreateLayout(gui, 0.5f, 0.2f, "Vertical: H=0.5, V=0.2", Color.LightSeaGreen, Axis.Vertical);
            }

            // Test horizontal layouts with separate values
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
            {
                CreateLayout(gui, 0.0f, 0.0f, "Horizontal: AlignContent(0.0)", Color.LightBlue, Axis.Horizontal);
                CreateLayout(gui, 0.5f, 0.5f, "Horizontal: AlignContent(0.5)", Color.LightGreen, Axis.Horizontal);
                CreateLayout(gui, 1.0f, 1.0f, "Horizontal: AlignContent(1.0)", Color.LightCoral, Axis.Horizontal);
            }

            // Test separate horizontal/vertical values
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
            {
                CreateLayout(gui, 0.0f, 1.0f, "Horizontal: H=0.0, V=1.0", Color.LightSalmon, Axis.Horizontal);
                CreateLayout(gui, 1.0f, 0.0f, "Horizontal: H=1.0, V=0.0", Color.LightSteelBlue, Axis.Horizontal);
                CreateLayout(gui, 0.5f, 0.2f, "Horizontal: H=0.5, V=0.2", Color.LightSeaGreen, Axis.Horizontal);
            }
        }
    }

    private static void CreateLayout(Gui gui, float alignHorizontal, float alignVertical, string title, Color backgroundColor, Axis axis)
    {
        using (gui.Node().ExpandWidth().Height(150).Margin(5).Enter())
        {
            gui.DrawBackgroundRect(backgroundColor, radius: 5);

            // Title
            using (gui.Node().Height(25).Enter())
            {
                gui.DrawText(title, color: Color.Black, size: 12);
            }

            // Container with vertical layout and AlignContent
            using (gui.Node().Expand().Gap(5).Margin(10).Direction(axis).AlignContent(alignHorizontal, alignVertical).Enter())
            {
                gui.DrawBackgroundRect(Color.White, radius: 3);

                // Create 3 tiny children
                for (var i = 0; i < 3; i++)
                {
                    using (gui.Node(30, 15).Enter())
                    {
                        var color = i switch { 0 => Color.Red, 1 => Color.Orange, _ => Color.Green };
                        gui.DrawBackgroundRect(color);
                        gui.DrawText($"{i + 1}", color: Color.White, size: 14);
                    }
                }
            }
        }
    }
}
