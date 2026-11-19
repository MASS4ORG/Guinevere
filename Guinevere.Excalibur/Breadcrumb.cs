using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Horizontal padding inside a crumb, leaving room for the hover underline.</summary>
    const float CrumbPadding = 8f;

    /// <summary>Vertical padding inside a crumb and its separator, which sets the bar's height around the text.</summary>
    const float CrumbPaddingY = 4f;

    /// <summary>The chevron drawn between crumbs.</summary>
    const string Chevron = "›";

    static readonly string[] LinkModifier = ["link"];
    static readonly string[] CurrentModifier = ["current"];

    sealed class BreadcrumbMenuState
    {
        public bool IsOpen { get; set; }

        /// <summary>Where the "›" sat last frame, which is where the children menu anchors.</summary>
        public Vector2 Anchor { get; set; }
    }

    /// <summary>
    /// Draws a breadcrumb trail from left to right. Every crumb with an
    /// <see cref="BreadcrumbItem.OnClick"/> that is not marked <see cref="BreadcrumbItem.IsCurrent"/> is
    /// a link: hovered it highlights, clicked it invokes the action, and a focused crumb fires on
    /// Space/Enter. The current page is drawn as plain non-interactive text. A crumb shows any
    /// <see cref="Content"/> and may carry a set of <see cref="BreadcrumbItem.Children"/>; when
    /// children are present the "›" to the crumb's left opens a context menu listing them. The bar fits its crumbs;
    /// the text takes the control font size unless a sheet sets one.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="items">The trail, root first. The last item is usually the current page.</param>
    /// <param name="classes">Stylesheet classes for the breadcrumb.</param>
    /// <param name="id">Optional id for <c>breadcrumb#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The bar's node, <see cref="LayoutNode.Clicked"/> (and as a <c>bool</c>) on a frame a crumb or a menu entry ran its action.</returns>
    public static ControlNode Breadcrumb(this Gui gui, IReadOnlyList<BreadcrumbItem> items,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(items);

        ExcaliburStyles.Ensure(gui);
        var bar = gui.StyledControl("breadcrumb", classes, id, filePath: filePath, lineNumber: lineNumber);
        UseControlFontSize(gui, bar);
        FitUnlessStyled(bar);
        bar.Direction(Axis.Horizontal).AlignContentY(0.5f);

        using (bar.Enter())
        {
            for (var i = 0; i < items.Count; i++)
            {
                using var key = gui.ItemKey(i);
                if (i > 0)
                {
                    RenderBreadcrumbSeparator(gui, bar, items[i - 1], i);
                }

                RenderBreadcrumbCrumb(gui, bar, items[i]);
            }
        }

        return bar;
    }

    static void RenderBreadcrumbCrumb(Gui gui, LayoutNode bar, BreadcrumbItem item)
    {
        var interactive = item is { IsCurrent: false, OnClick: not null };

        var crumb = gui.StyledNode("crumb", modifiers: interactive ? LinkModifier : CurrentModifier);
        FitUnlessStyled(crumb);
        PadUnlessStyled(crumb, CrumbPadding, CrumbPaddingY);
        using (crumb.Direction(Axis.Horizontal).AlignContent(0.5f, 0.5f).Enter())
        {
            gui.Content(item.Content);

            if (gui.Pass == Pass.Pass2Render && interactive)
            {
                HandleBreadcrumbCrumbInteraction(gui, bar, item);
            }
        }
    }

    /// <summary>Handles a link crumb's focus, click and hover underline.</summary>
    static void HandleBreadcrumbCrumbInteraction(Gui gui, LayoutNode bar, BreadcrumbItem item)
    {
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var hovered = gui.GetInteractable().OnHover();
        var clicked = Activated(gui);
        if (clicked)
        {
            bar.Raise(NodeSignals.Clicked);
            item.OnClick?.Invoke();
        }

        if (hovered || clicked || gui.HasFocus())
        {
            DrawBreadcrumbUnderline(gui);
        }
    }

    static void DrawBreadcrumbUnderline(Gui gui)
    {
        var rect = gui.CurrentNode.Rect;
        gui.DrawStyledBox(gui.ResolvePart("underline"),
            new Rect(rect.X + (CrumbPadding * 0.5f), rect.Y + rect.H - 3.5f, rect.W - CrumbPadding, 1.5f));
    }

    /// <summary>
    /// The chevron between crumbs. When the crumb to its left carries children it is clickable: a
    /// hover highlights it and a click opens a context menu listing those children under the bar.
    /// </summary>
    static void RenderBreadcrumbSeparator(Gui gui, LayoutNode bar, BreadcrumbItem leftItem, int index)
    {
        var hasChildren = leftItem.Children is { Count: > 0 };
        var state = gui.ControlState($"{bar.Id}/menu/{index}", static () => new BreadcrumbMenuState());

        var clicked = RenderBreadcrumbChevron(gui, state, hasChildren);
        if (hasChildren)
        {
            RenderBreadcrumbChildrenMenu(gui, bar, leftItem, state, clicked);
        }
    }

    /// <summary>Draws the chevron, toggles the children menu on click and returns true when clicked.</summary>
    static bool RenderBreadcrumbChevron(Gui gui, BreadcrumbMenuState state, bool hasChildren)
    {
        var clicked = false;

        var separator = gui.StyledNode("crumb-separator",
            modifiers: hasChildren && state.IsOpen ? OpenModifier : NoModifiers);
        FitUnlessStyled(separator);
        using (separator.Margin(2, 0).Padding(0, CrumbPaddingY).AlignContent(0.5f, 0.5f).Enter())
        {
            gui.DrawText(Chevron, centerInRect: true);

            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                state.Anchor = new Vector2(rect.X, rect.Y + rect.H);
                if (hasChildren)
                {
                    gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                    clicked = Activated(gui);
                }
            }

            if (clicked)
            {
                state.IsOpen = !state.IsOpen;
            }
        }

        return clicked;
    }

    /// <summary>Lists a crumb's children in a context menu anchored at its chevron.</summary>
    static void RenderBreadcrumbChildrenMenu(Gui gui, LayoutNode bar, BreadcrumbItem leftItem,
        BreadcrumbMenuState state, bool clicked)
    {
        // Menu opening is deferred by one frame: the press that opened it must not be read on that
        // same frame as a click outside the menu, which would dismiss it before it ever shows.
        var isOpen = state.IsOpen && !clicked;
        gui.ContextMenu(ref isOpen, builder =>
        {
            foreach (var child in leftItem.Children!)
            {
                builder.Item(child.Content.Label ?? "", () =>
                {
                    bar.Raise(NodeSignals.Clicked);
                    child.OnClick?.Invoke();
                });
            }
        }, position: state.Anchor);
        if (!clicked)
        {
            state.IsOpen = isOpen;
        }
    }
}
