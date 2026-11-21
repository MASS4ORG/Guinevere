namespace Guinevere;

public static partial class ControlsExtensions
{
    const float TabCloseButtonSize = 18f;
    const float TabCloseFontSize = 12f;
    const float TabCloseGap = 6f;

    /// <summary>
    /// The "×" of a closable tab, a <c>tab-close</c> node sized unless the sheet sets it. A click requests the
    /// close. Returns whether the pointer is over it, so the tab does not also take the click as a selection.
    /// </summary>
    static bool RenderTabCloseButton(Gui gui, TabsState state, int tabIndex)
    {
        var tab = state.Tabs[tabIndex];
        var node = gui.StyledNode("tab-close");
        SizeUnlessStyled(node, Size.Pixels(TabCloseButtonSize), Size.Pixels(TabCloseButtonSize));
        using (node.AlignContent(0.5f).Enter())
        {
            if (!node.Scope.HasLocal<LayoutNodeScopeTextSize>())
            {
                gui.SetTextSize(TabCloseFontSize);
            }

            gui.DrawText("×");
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            var interactable = gui.GetInteractable();
            if (interactable.OnClick() && tab.Enabled)
            {
                state.TabToClose = (tabIndex, tab.Key!);
            }

            return interactable.OnHover();
        }
    }
}
