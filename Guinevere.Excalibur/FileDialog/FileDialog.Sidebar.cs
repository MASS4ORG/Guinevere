namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// The shortcuts column. Each entry jumps to its directory; the one containing what is being
    /// browsed is marked, so the sidebar shows where the listing is.
    /// </summary>
    static void Sidebar(Gui gui, FileDialogState state, float fontSize)
    {

        using (gui.StyledNode("file-sidebar").Width(SidebarWidth).ExpandHeight().Enter())
        {

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
        var current = string.Equals(state.Browser.CurrentPath, place.Path, StringComparison.OrdinalIgnoreCase);

        using (gui.StyledNode("file-place", id: state.ControlId($"place/{place.Path}"),
                       modifiers: current ? ["selected"] : []).Height(RowHeight).ExpandWidth()
                   .Direction(Axis.Horizontal).Gap(6f).PaddingX(6f).ContentAlignY(0.5f).Enter())
        {
            var interactable = gui.GetInteractable();


            gui.DrawText(place.Icon, fontSize, gui.ResolvePart("file-detail").GetColor("color"));

            using (gui.Node().Expand().ContentAlignY(0.5f).Enter())
            {
                gui.ClipContent();
                gui.DrawText(place.Label, fontSize, centerInRect: false, clip: true);
            }

            gui.Tooltip(gui.CurrentNode, place.Path, maxWidth: 600);

            if (gui.Pass != Pass.Pass2Render || !interactable.OnClick()) return;

            _ = state.Browser.NavigateAsync(place.Path);
            state.Selected = null;
            state.Message = null;
        }
    }
}
