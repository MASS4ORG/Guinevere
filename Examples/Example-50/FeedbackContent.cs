using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    static void FeedbackContent(Gui gui)
    {
        gui.SetTextColor(Token(gui, "text"));
        using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(16).Padding(10).Enter())
        {
            Section(gui, "Toasts ", () => ToastContent(gui));
            Section(gui, "Tooltips", () => TooltipContent(gui));
            Section(gui, "Progress bar", () => ProgressBarContent(gui));
        }
    }

    static void ToastContent(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(10).Enter())
        {
            using (gui.Node().Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter())
            {
                if (gui.Button("Info BottomRight", classes: ["accent"]))
                    gui.Toast("Here is some information", new ToastOptions
                    {
                        Corner = ToastCorner.BottomRight,
                        Classes = ["accent"]
                    });

                if (gui.Button("Success BottomRight", classes: ["success"]))
                    gui.Toast("Operation completed successfully!", new ToastOptions
                    {
                        Corner = ToastCorner.BottomRight,
                        Classes = ["positive"]
                    });

                if (gui.Button("Warning TopRight", classes: ["warning"]))
                    gui.Toast("Something needs your attention", new ToastOptions
                    {
                        Corner = ToastCorner.TopRight,
                        Classes = ["warning"]
                    });

                if (gui.Button("Danger BottomLeft", classes: ["danger"]))
                    gui.Toast("That action could not be completed", new ToastOptions
                    {
                        Corner = ToastCorner.BottomLeft,
                        Classes = ["negative"]
                    });

                if (gui.Button("Note TopLeft", classes: ["info"]))
                    gui.Toast("A reminder pinned to the top-left corner", new ToastOptions
                    {
                        Corner = ToastCorner.TopLeft,
                        Classes = ["info"]
                    });

                if (gui.Button("Clear"))
                    gui.ClearToasts();
            }
        }
    }

    static void TooltipContent(Gui gui)
    {
        if (gui.Button("Hover me for a tooltip", width: 180, height: 28))
            gui.Toast("You clicked the tooltip button");

        gui.Tooltip(gui.CurrentNode, "Sticking around on a button reveals this hint", delay: 0.35f);
    }

    static void ProgressBarContent(Gui gui)
    {
        using (gui.Node().Direction(Axis.Vertical).Gap(12).Enter())
        {
            // The bars move with time alone, so the section keeps asking for frames.
            gui.RequestFrame();
            var fraction = gui.Time.Elapsed * 0.25f % 1f;

            using (gui.Node().Height(12).Direction(Axis.Horizontal).Gap(10).Enter())
            {
                gui.ProgressBar(fraction, height: 12);

                gui.DrawText($"{fraction:P0}", size: 12, color: Token(gui, "accent"),
                    centerInRect: false);
            }

            using (gui.Node().Expand().Enter())
            {
                gui.SetStyleToken("accent", Hsb(fraction * 240f));
                gui.ProgressBar(fraction, height: 10);
            }

            using (gui.Node().ExpandWidth().Enter())
            {
                gui.SetStyleToken("divider", Color.FromArgb(255, 224, 224, 224));
                gui.SetStyleToken("accent", Color.FromArgb(255, 76, 175, 80));
                gui.ProgressBar(null, height: 14);
            }
        }
    }
}
