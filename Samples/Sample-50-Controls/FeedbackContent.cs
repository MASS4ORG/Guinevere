using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    private static void FeedbackContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(16).Padding(10).Enter())
        {
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

            Section(gui, "Custom Colors & Sizes", () =>
            {
                using (gui.Node().Direction(Axis.Vertical).Gap(10).Enter())
                {
                    gui.ProgressBar(0.82f, height: 4, fillColor: Color.FromArgb(255, 156, 39, 176));
                    gui.ProgressBar(0.82f, height: 8, fillColor: Color.FromArgb(255, 156, 39, 176));
                    gui.ProgressBar(0.82f, height: 16,
                        trackColor: Color.FromArgb(255, 238, 238, 238),
                        fillColor: Color.FromArgb(255, 156, 39, 176));
                    gui.ProgressBar(0.5f, height: 12,
                        trackColor: Color.FromArgb(255, 255, 235, 59),
                        fillColor: Color.FromArgb(255, 244, 67, 54));
                }
            });
        }
    }
}