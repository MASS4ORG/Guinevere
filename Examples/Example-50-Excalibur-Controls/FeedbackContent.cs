using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static void FeedbackContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(16).Padding(10).Enter())
        {
            Section(gui, "Toasts & Tooltips", () => ToastTooltipContent(gui));

            Section(gui, "Determinate Progress", () =>
            {
                using (gui.Node().Direction(Axis.Vertical).Gap(12).Enter())
                {
                    gui.DrawText("Animated — sweeps from empty to full and back", size: 13,
                        color: Color.FromArgb(255, 102, 102, 102));

                    gui.ProgressBar((MathF.Sin(gui.Time.Elapsed) + 1f) / 2f, height: 10);

                    gui.Node(0, 6);

                    gui.DrawText("Animated — fills left to right, the label shows the fill", size: 13,
                        color: Color.FromArgb(255, 102, 102, 102));

                    var fraction = gui.Time.Elapsed * 0.25f % 1f;

                    using (gui.Node().Height(12).Direction(Axis.Horizontal).Gap(10).Enter())
                    {
                        using (gui.Node().Expand().Enter())
                            gui.ProgressBar(fraction, height: 12);

                        gui.DrawText($"{fraction:P0}", size: 12, color: Color.FromArgb(255, 63, 81, 181),
                            centerInRect: false);
                    }

                    gui.Node(0, 6);

                    gui.DrawText("Fixed values with hue-shifted fills", size: 13,
                        color: Color.FromArgb(255, 102, 102, 102));

                    for (var i = 0; i < 4; i++)
                    {
                        var value = (i + 1) * 0.2f;

                        using (gui.Node().Height(10).Direction(Axis.Horizontal).Gap(10).Enter())
                        {
                            using (gui.Node(60).Enter())
                                gui.DrawText($"{value:P0}".PadLeft(4), size: 12, color: Color.Gray,
                                    centerInRect: false);

                            using (gui.Node().Expand().Enter())
                                gui.ProgressBar(value, height: 10, fillColor: Hsb(value * 240f));
                        }
                    }
                }
            });

            Section(gui, "Indeterminate Progress", () =>
            {
                using (gui.Node().Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.DrawText("Unknown duration — a chunk travels the track instead of a fill", size: 13,
                        color: Color.FromArgb(255, 102, 102, 102));

                    gui.ProgressBar(null, height: 6);
                    gui.ProgressBar(null, height: 10, fillColor: Hsb(220f));
                    gui.ProgressBar(null, height: 14,
                        trackColor: Color.FromArgb(255, 224, 224, 224),
                        fillColor: Color.FromArgb(255, 76, 175, 80));
                }
            });
        }
    }

    static void ToastTooltipContent(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(10).Enter())
        {
            gui.DrawText(
                "Toasts are transient, corner-pinned notifications. Re-triggering the same one " +
                "refreshes its lifetime instead of stacking. Click a toast to dismiss it.",
                size: 12, color: Color.FromArgb(255, 102, 102, 102), wrapWidth: 620);

            using (gui.Node().Direction(Axis.Horizontal).Gap(8).Enter())
            {
                if (gui.Button("Info", width: 80, height: 26))
                    gui.Toast("Here is some information", new ToastOptions
                    {
                        Corner = ToastCorner.BottomRight,
                        AccentColor = Color.FromArgb(255, 33, 150, 243)
                    });

                if (gui.Button("Success", width: 80, height: 26))
                    gui.Toast("Operation completed successfully!", new ToastOptions
                    {
                        Corner = ToastCorner.BottomRight,
                        AccentColor = Color.FromArgb(255, 76, 175, 80)
                    });

                if (gui.Button("Warning", width: 80, height: 26))
                    gui.Toast("Something needs your attention", new ToastOptions
                    {
                        Corner = ToastCorner.TopRight,
                        AccentColor = Color.FromArgb(255, 255, 152, 0)
                    });

                if (gui.Button("Danger", width: 80, height: 26))
                    gui.Toast("That action could not be completed", new ToastOptions
                    {
                        Corner = ToastCorner.BottomLeft,
                        AccentColor = Color.FromArgb(255, 229, 57, 53)
                    });

                if (gui.Button("Note", width: 80, height: 26))
                    gui.Toast("A reminder pinned to the top-left corner", new ToastOptions
                    {
                        Corner = ToastCorner.TopLeft,
                        AccentColor = Color.FromArgb(255, 0, 150, 136)
                    });

                if (gui.Button("Clear", width: 80, height: 26))
                    gui.ClearToasts();
            }

            gui.Node(0, 6);

            gui.DrawText("Hover the button below — its tooltip appears after a short delay.",
                size: 12, color: Color.FromArgb(255, 102, 102, 102));

            LayoutNode tooltipAnchor;
            using (gui.Node(180, 28).Enter())
            {
                tooltipAnchor = gui.CurrentNode;

                if (gui.Button("Hover me for a tooltip", width: 180, height: 28))
                    gui.Toast("You clicked the tooltip button");
            }

            gui.Tooltip(tooltipAnchor, "Sticking around on a button reveals this hint",
                delay: 0.35f);

            gui.Toasts();
        }
    }
}
