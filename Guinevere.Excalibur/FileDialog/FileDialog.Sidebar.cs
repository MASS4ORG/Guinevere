namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// The shortcuts column. Each entry jumps to its directory; the one containing what is being
    /// browsed is marked, so the sidebar shows where the listing is.
    /// </summary>
    static void Sidebar(Gui gui, FileDialogState state, float fontSize)
    {
        var palette = gui.ControlStyle;

        using (gui.Node(sidebarWidth, -1).ExpandHeight().Enter())
        {
            if (gui.Pass == Pass.Pass2Render) gui.DrawBackgroundRect(palette.Surface, 4f);

            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(1f).Padding(4f).Enter())
            {
                gui.ScrollY();

                foreach (var place in state.Places)
                    Place(gui, state, place, fontSize);
            }
        }
    }

    static void Place(Gui gui, FileDialogState state, FilePlace place, float fontSize)
    {
        var palette = gui.ControlStyle;
        var current = string.Equals(state.Browser.CurrentPath, place.Path, StringComparison.OrdinalIgnoreCase);

        using (gui.Node(-1, rowHeight, state.ControlId($"place/{place.Path}")).ExpandWidth()
                   .Direction(Axis.Horizontal).Gap(6f).PaddingX(6f).ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();

            if (gui.Pass == Pass.Pass2Render)
            {
                if (current) gui.DrawBackgroundRect(palette.Selected, 3f);
                else if (interactable.OnHover()) gui.DrawBackgroundRect(palette.SurfaceHover, 3f);
            }

            gui.DrawText(place.Icon, fontSize, palette.TextDim);

            using (gui.Node().Expand().ContentAlignY(0.5f).Enter())
            {
                gui.ClipContent();
                gui.DrawText(place.Label, fontSize, current ? palette.Text : palette.TextDim,
                    centerInRect: false, clip: true);
            }

            gui.Tooltip(gui.CurrentNode, place.Path, maxWidth: 600);

            if (gui.Pass != Pass.Pass2Render || !interactable.OnClick()) return;

            _ = state.Browser.NavigateAsync(place.Path);
            state.Selected = null;
            state.Message = null;
        }
    }
}
