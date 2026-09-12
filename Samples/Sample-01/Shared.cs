using Guinevere;
using GuinevereDemos;

namespace Sample_01;

public class Shared(Gui gui)
{
    public void Draw()
    {
        DemoHeader.Header(gui, "Guinevere");

        if (gui.Pass == Pass.Pass2Render && gui.Time.Elapsed % 1 < Math.Min(gui.Time.DeltaTime, .01f))
        {
            Console.WriteLine($"FPS {(gui.Time.SmoothFps):N0}");
        }

        // 1. Test absolute positioning with fixed rectangles and FPS display
        gui.DrawRect(new(0, 0, 150, 150), Color.DeepPink);
        gui.DrawCircle(new(35.5f, 35.5f), 35.5f, Color.BlueViolet);
        gui.DrawCircle(new(25, 25), 25, Color.BlueViolet);
        gui.SetTextColor(Color.White);

        // 2. Test the horizontal layout container with margins, and content alignment
        using (gui.Node().Expand()
                   .Margin(100)
                   .Direction(Axis.Horizontal)
                   .Gap(10)
                   .AlignContent(.5f)
                   .Enter())
        {
            gui.DrawBackgroundRect(Color.Crimson, radius: 20, Corner.Bottom);

            gui.SetZIndex(1);

            // 3. Test nested node with margin/padding combinations
            using (gui
                       .Node(75, 75)
                       .Margin(10, 20)
                       .Padding(10)
                       .Enter())
            {
                gui.DrawBackgroundRect(Color.Green);
            }

            // 4. Test conditional rendering based on input
            if (gui.Input.IsKeyDown(KeyboardKey.LeftShift))
            {
                using (gui
                           .Node(100, 100)
                           .Enter())
                {
                    gui.DrawBackgroundRect(Color.White, radius: 15);
                }
            }

            // 5. Test hover/click interactions with state-based styling
            using (gui
                       .Node(150, 150)
                       .Enter())
            {
                if (gui.GetInteractable().OnHover())
                {
                    gui.DrawBackgroundRect(Color.DarkRed, radius: 10);
                }
                else
                {
                    gui.DrawBackgroundRect(Color.Orange, radius: 2);
                }

                if (gui.GetInteractable().OnClick())
                {
                    gui.DrawBackgroundRect(Color.Yellow, radius: 2);
                    gui.DrawText("You really clicked!");
                    Console.WriteLine("You really clicked!");
                }
                else
                {
                    gui.DrawText("Click me");
                }
            }

            // 6. Test vertical layout nesting within the horizontal parent
            using (gui
                       .Node(200, 200)
                       .Margin(10, 20)
                       .Direction(Axis.Vertical)
                       .AlignContent(.5f)
                       .Enter())
            {
                gui.DrawBackgroundRect(Color.Purple, radius: 15, corners: Corner.Bottom);
                if (gui.GetInteractable().OnHover())
                {
                    gui.DrawBackgroundRect(Color.DeepPink, radius: 10);
                }

                gui.DrawText("Press 'Shift'", color: Color.Yellow);

                // 7. Test 3rd level nesting with margin/padding
                using (gui
                           .Node(50, 50)
                           .Margin(10, 20)
                           .Enter())
                {
                    gui.DrawBackgroundRect(Color.Green);
                }
            }

            // 8. Test negative z-index (should be hidden behind the parent container)
            using (gui
                       .Node(100, 100)
                       .Enter())
            {
                gui.SetZIndex(-1);
                gui.DrawBackgroundRect(Color.Blue);
                gui.DrawText("zIndex -1", 15);
            }
        }
    }
}
