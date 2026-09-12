using Guinevere;
using Guinevere.OpenGL.SilkNET;
using Axis = Guinevere.Axis;

namespace Sample_03_ChildrenLayout;

public abstract class Program
{
    private static int _activeTabIndex;

    private static readonly Color Panel = Color.FromArgb(255, 46, 50, 62);
    private static readonly Color Bar = Color.FromArgb(255, 90, 170, 255);

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 800, 800, "Children Layout Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawWindowTitlebar();

        using (gui.Node().Expand().Enter())
        {
            gui.Tabs(ref _activeTabIndex, tabs =>
            {
                tabs.Tab("Children", () => ChildrenLayout(gui));
                tabs.Tab("Constrains", () => Constrains(gui));
            });
        }
    }

    private static void ChildrenLayout(Gui gui)
    {
        // Test AlignContent with separate horizontal/vertical values
        using (gui.Node().Expand().Margin(10).Direction(Axis.Vertical).Gap(20).Enter())
        {
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

    private static void CreateLayout(Gui gui, float alignHorizontal, float alignVertical, string title,
        Color backgroundColor, Axis axis)
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
            using (gui.Node().Expand().Gap(5).Margin(10).Direction(axis).AlignContent(alignHorizontal, alignVertical)
                       .Enter())
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


    private static void Constrains(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(20).Enter())
        {
            gui.DrawText("WidthPercent — 25% / 50% / 75% / 100% of the container", 15);
            using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(8).Padding(12).Enter())
            {
                gui.DrawBackgroundRect(Panel, radius: 8);
                foreach (var f in new[] { 0.25f, 0.5f, 0.75f, 1.0f })
                    using (gui.Node(height: 24).WidthPercent(f).Enter())
                        gui.DrawBackgroundRect(Bar, radius: 6);
            }

            gui.DrawText("MaxWidth(420) — an Expand child that stops growing", 15);
            using (gui.Node(height: 36).ExpandWidth().MaxWidth(420f).Enter())
            {
                gui.DrawBackgroundRect(Bar, radius: 6);
                gui.DrawText("420px max").Expand();
            }

            gui.DrawText("MinWidth(150) — chips forced to a minimum width", 15);
            using (gui.Node(height: 44).ExpandWidth().Direction(Axis.Horizontal).Gap(12).Enter())
            {
                foreach (var label in new[] { "A", "BB", "CCC" })
                    using (gui.Node(height: 44).MinWidth(150f).Enter())
                    {
                        gui.DrawBackgroundRect(Panel, radius: 8);
                        gui.DrawText(label, 15, Bar).Expand();
                    }
            }

            gui.DrawText("HeightPercent(0.6) — fills 60% of the remaining column", 15);
            using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Padding(10).ExpandHeight().Enter())
            {
                gui.DrawBackgroundRect(Panel, radius: 8);
                using (gui.Node().ExpandWidth().HeightPercent(0.6f).Enter())
                {
                    gui.DrawBackgroundRect(Bar, radius: 8);
                    gui.DrawText("60%").Expand();
                }
            }
        }
    }
}
