namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed record MenuAppearance(float ItemHeight = 26, float SeparatorHeight = 9, float MinWidth = 160,
        float Radius = 4, Color? Border = null, Color? Separator = null, Color? Disabled = null);

    sealed class MenuBranch
    {
        public int OpenIndex = -1;
        public float LastInside;
        public Rect Rect;
    }

    static void PreparePopupMenuFrame(Gui gui, MenuBarState state, bool isOpen, MenuAppearance? appearance = null)
    {
        if (gui.Pass != Pass.Pass1Build) return;
        if (!isOpen) ResetMenuState(state);
        state.OpenIndex = isOpen ? 0 : -1;
        state.SubmenuRects = [];
        state.Appearance = appearance ?? state.Appearance;
        state.BeginFrame(gui.Input.MousePosition);
    }

    static int ResolveMenuBranch(Gui gui, MenuBarState state, string id, List<FlyoutItem> items,
        Rect rect, int hovered, int depth)
    {
        if (gui.Pass != Pass.Pass1Build) return state.FrameBranches.GetValueOrDefault(id, -1);
        if (!state.Branches.TryGetValue(id, out var branch))
            state.Branches[id] = branch = new MenuBranch();
        branch.Rect = rect;
        var index = UpdateMenuBranch(gui, state, id, items, hovered, depth, branch);
        state.FrameBranches[id] = index;
        return index;
    }

    static int UpdateMenuBranch(Gui gui, MenuBarState state, string id, List<FlyoutItem> items,
        int hovered, int depth, MenuBranch branch)
    {
        if (state.FrameKeyboardActive)
        {
            branch.OpenIndex = state.FrameKeyboardPath.Count > depth + 1
                ? state.FrameKeyboardPath[depth] : -1;
            return branch.OpenIndex;
        }

        if (!IsValidSubmenu(items, branch.OpenIndex)) branch.OpenIndex = -1;
        if (branch.OpenIndex >= 0 && KeepMenuBranchOpen(gui, state, id, items, hovered, branch))
            return branch.OpenIndex;
        var candidate = IsValidSubmenu(items, hovered) ? hovered : -1;
        ChangeMenuBranch(state, id, branch, candidate);
        branch.LastInside = gui.Clock.Elapsed;
        return branch.OpenIndex;
    }

    static bool IsValidSubmenu(List<FlyoutItem> items, int index) => index >= 0 && index < items.Count
        && items[index].Enabled && items[index].HasSubmenu;

    static bool KeepMenuBranchOpen(Gui gui, MenuBarState state, string id, List<FlyoutItem> items,
        int hovered, MenuBranch branch)
    {
        var childId = $"{id}/{items[branch.OpenIndex].Text}";
        var insideChild = state.Branches.Any(pair =>
            (pair.Key == childId || pair.Key.StartsWith(childId + "/", StringComparison.Ordinal))
            && pair.Value.Rect.Contains(gui.Input.MousePosition));
        if (hovered == branch.OpenIndex || insideChild)
        {
            branch.LastInside = gui.Clock.Elapsed;
            return true;
        }
        // Delay switching as the pointer crosses neighboring rows on its way to the child panel.
        return !gui.Input.IsMouseButtonPressed(MouseButton.Left)
            && gui.Clock.Elapsed - branch.LastInside < 0.3f;
    }

    static void ChangeMenuBranch(MenuBarState state, string id, MenuBranch branch, int candidate)
    {
        if (candidate == branch.OpenIndex) return;
        foreach (var descendant in state.Branches.Keys
                     .Where(key => key.StartsWith(id + "/", StringComparison.Ordinal)).ToArray())
            state.Branches.Remove(descendant);
        branch.OpenIndex = candidate;
    }

    static Vector2 SubmenuPosition(Gui gui, List<FlyoutItem> items, Rect parent, float rowOffset,
        float fontSize, float padding, float minWidth)
    {
        var width = CalculateFlyoutWidth(items, fontSize, padding, minWidth, items.Any(item => item.IsChecked != null));
        var right = parent.X + parent.W;
        var x = right + width > gui.ScreenRect.X + gui.ScreenRect.W && parent.X - width >= gui.ScreenRect.X
            ? parent.X - width : right;
        return new Vector2(x, parent.Y + rowOffset);
    }

    static List<FlyoutItem> KeyboardMenu(MenuBarState state, List<FlyoutItem> root)
    {
        var items = root;
        for (var depth = 0; depth < state.KeyboardPath.Count - 1; depth++)
        {
            var index = state.KeyboardPath[depth];
            if (index < 0 || index >= items.Count || items[index].Submenu is not { } child)
            {
                state.KeyboardPath.RemoveRange(depth, state.KeyboardPath.Count - depth);
                break;
            }
            items = child;
        }
        return items;
    }

    static void ActivateMenuRow(MenuBarState state, FlyoutItem item, int depth, int index)
    {
        if (!item.Enabled || item.IsSeparator) return;
        if (item.HasSubmenu)
        {
            while (state.KeyboardPath.Count <= depth) state.KeyboardPath.Add(-1);
            state.KeyboardPath[depth] = index;
            state.KeyboardPath.RemoveRange(depth + 1, state.KeyboardPath.Count - depth - 1);
            state.KeyboardPath.Add(NextSelectableIndex(item.Submenu!, -1, 1));
            state.KeyboardActive = true;
        }
        else ActivateRow(state, item);
    }

    static void DismissPopupMenuOutside(Gui gui, MenuBarState state, bool rightClick)
    {
        var pressed = gui.Input.IsMouseButtonPressed(MouseButton.Left)
            || (rightClick && gui.Input.IsMouseButtonPressed(MouseButton.Right));
        if (pressed && !state.SubmenuRects.Any(rect => rect.Contains(gui.Input.MousePosition)))
            ResetMenuState(state);
    }
}
