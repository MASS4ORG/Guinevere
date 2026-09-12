namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Horizontal padding inside a crumb, leaving room for the hover underline.</summary>
    private const float CrumbPadding = 8f;

    /// <summary>The chevron drawn between crumbs.</summary>
    private const string Chevron = "›";

    /// <summary>
    /// Draws a breadcrumb trail from left to right. Every crumb with an
    /// <see cref="BreadcrumbItem.OnClick"/> that is not marked <see cref="BreadcrumbItem.IsCurrent"/> is
    /// a link: hovered it highlights, clicked it invokes the action, and a focused crumb fires on
    /// Space/Enter. The current page is drawn as plain non-interactive text.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="items">The trail, root first. The last item is usually the current page.</param>
    /// <param name="height">Height of the bar.</param>
    /// <param name="fontSize">Crumb and chevron size.</param>
    /// <param name="linkColor">Colour of an interactive crumb; defaults to the palette's accent.</param>
    /// <param name="linkHoverColor">Colour of a hovered or keyboard-activated crumb; defaults to the
    /// palette's selected colour.</param>
    /// <param name="currentColor">Colour of the current page; defaults to the palette's text.</param>
    /// <param name="separatorColor">Colour of the chevrons; defaults to the palette's dim text.</param>
    public static void Breadcrumb(this Gui gui,
        IReadOnlyList<BreadcrumbItem> items,
        float height = 28,
        float fontSize = 13,
        Color? linkColor = null,
        Color? linkHoverColor = null,
        Color? currentColor = null,
        Color? separatorColor = null)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return;

        var palette = gui.Controls;
        var link = linkColor ?? palette.Accent;
        var linkHovered = linkHoverColor ?? palette.Selected;
        var current = currentColor ?? palette.Text;
        var separator = separatorColor ?? palette.TextDim;

        var font = new SKFont { Size = fontSize };

        using (gui.Node(-1, height).Direction(Axis.Horizontal).Enter())
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) RenderBreadcrumbSeparator(gui, font, height, separator);
                RenderBreadcrumbCrumb(gui, items[i], i, font, height, link, linkHovered, current);
            }
        }
    }

    private static void RenderBreadcrumbCrumb(Gui gui, BreadcrumbItem item, int index, SKFont font,
        float height, Color link, Color linkHovered, Color current)
    {
        var interactive = !item.IsCurrent && item.OnClick is not null;
        var width = MeasureTextWidth(font, item.Label) + (CrumbPadding * 2);

        using (gui.Node(width, height, $"breadcrumb/{index}").AlignContent(0.5f, 0.5f).Enter())
        {
            var hovered = false;
            var clicked = false;

            if (gui.Pass == Pass.Pass2Render && interactive)
            {
                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                var interactable = gui.GetInteractable();
                hovered = interactable.OnHover();

                if (interactable.OnClick())
                {
                    gui.RequestFocus(FocusReason.Mouse);
                    clicked = true;
                }
                else if (gui.HasFocus() && (gui.Input.IsKeyPressed(KeyboardKey.Space) ||
                                            gui.Input.IsKeyPressed(KeyboardKey.Enter)))
                {
                    clicked = true;
                }
            }

            gui.DrawText(item.Label, font.Size,
                interactive ? (hovered || clicked ? linkHovered : link) : current, centerInRect: true);

            if (interactive && clicked) item.OnClick?.Invoke();

            if (gui.Pass == Pass.Pass2Render && interactive && (hovered || clicked || gui.HasFocus()))
                DrawBreadcrumbUnderline(gui, hovered || clicked ? linkHovered : link);
        }
    }

    private static void DrawBreadcrumbUnderline(Gui gui, Color color)
    {
        var rect = gui.CurrentNode.Rect;
        gui.DrawRect(new Rect(rect.X + (CrumbPadding * 0.5f), rect.Y + rect.H - 3.5f,
            rect.W - CrumbPadding, 1.5f), color);
    }

    private static void RenderBreadcrumbSeparator(Gui gui, SKFont font, float height, Color color)
    {
        using (gui.Node(MeasureTextWidth(font, Chevron), height, null).Margin(2, 0)
                   .AlignContent(0.5f, 0.5f).Enter())
        {
            gui.DrawText(Chevron, font.Size, color, centerInRect: true);
        }
    }
}