namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Horizontal padding inside a crumb, leaving room for the hover underline.</summary>
    const float CrumbPadding = 8f;

    /// <summary>The gap between a crumb's icon and its label.</summary>
    const float CrumbIconGap = 4f;

    /// <summary>The chevron drawn between crumbs.</summary>
    const string Chevron = "›";

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
    /// Space/Enter. The current page is drawn as plain non-interactive text. A crumb may carry an
    /// <see cref="BreadcrumbItem.Icon"/> and a set of <see cref="BreadcrumbItem.Children"/>; when
    /// children are present the "›" to the crumb's left opens a context menu listing them.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="items">The trail, root first. The last item is usually the current page.</param>
    /// <param name="height">Height of the bar.</param>
    /// <param name="fontSize">Crumb and chevron size.</param>
    /// <param name="linkColor">Color of an interactive crumb; defaults to the palette's accent.</param>
    /// <param name="linkHoverColor">Color of a hovered or keyboard-activated crumb; defaults to the
    /// palette's selected color.</param>
    /// <param name="currentColor">Color of the current page; defaults to the palette's text.</param>
    /// <param name="separatorColor">Color of the chevrons; defaults to the palette's dim text.</param>
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

        var palette = gui.ControlStyle;
        var link = linkColor ?? palette.Accent;
        var linkHovered = linkHoverColor ?? palette.Selected;
        var current = currentColor ?? palette.Text;
        var separator = separatorColor ?? palette.TextDim;

        var font = new SKFont { Size = fontSize };

        using (gui.Node(-1, height).Direction(Axis.Horizontal).Enter())
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) RenderBreadcrumbSeparator(gui, items[i - 1], i, font, height, separator, link);
                RenderBreadcrumbCrumb(gui, items[i], i, font, height, link, linkHovered, current);
            }
        }
    }

    static void RenderBreadcrumbCrumb(Gui gui, BreadcrumbItem item, int index, SKFont font,
        float height, Color link, Color linkHovered, Color current)
    {
        var interactive = item is { IsCurrent: false, OnClick: not null };
        var width = MeasureCrumbContent(gui, item, font.Size) + (CrumbPadding * 2);

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

            var color = interactive ? (hovered || clicked ? linkHovered : link) : current;
            if (gui.Pass == Pass.Pass2Render)
                DrawBreadcrumbContent(gui, item, font.Size, color);

            if (interactive && clicked) item.OnClick?.Invoke();

            if (gui.Pass == Pass.Pass2Render && interactive && (hovered || clicked || gui.HasFocus()))
                DrawBreadcrumbUnderline(gui, hovered || clicked ? linkHovered : link);
        }
    }

    /// <summary>The width the crumb content needs, measuring through the same main/icon font fallback
    /// that <see cref="Gui.DrawText"/> uses so an icon glyph sizes its share of the crumb correctly.</summary>
    static float MeasureCrumbContent(Gui gui, BreadcrumbItem item, float fontSize)
    {
        return MeasureCrumbPiece(gui, item.Icon ?? "", fontSize)
               + (item.Icon is null ? 0 : CrumbIconGap)
               + MeasureCrumbPiece(gui, item.Label, fontSize);
    }

    static float MeasureCrumbPiece(Gui gui, string text, float fontSize)
    {
        var width = 0f;
        foreach (var (runText, runFont) in TextRuns(gui, text, fontSize))
        {
            runFont.SkFont.MeasureText(runText, out var bounds);
            width += bounds.Width;
        }
        return width;
    }

    /// <summary>Draws the icon (if any) and the label as one run-split, vertically centered block.</summary>
    static void DrawBreadcrumbContent(Gui gui, BreadcrumbItem item, float fontSize, Color color)
    {
        var rect = gui.CurrentNode.Rect;
        var totalWidth = MeasureCrumbContent(gui, item, fontSize);
        var cursorX = rect.X + (rect.W - totalWidth) * 0.5f;

        if (item.Icon is not null)
            cursorX = DrawCrumbRuns(gui, item.Icon, fontSize, color, cursorX, rect);

        DrawCrumbRuns(gui, item.Label, fontSize, color, cursorX, rect);
    }

    static float DrawCrumbRuns(Gui gui, string text, float fontSize, Color color, float x, Rect rect)
    {
        var paint = new SKPaint { Color = color, IsAntialias = true };
        var runs = TextRuns(gui, text, fontSize);

        var ascent = 0f;
        var descent = 0f;
        foreach (var (runText, runFont) in runs)
        {
            runFont.SkFont.MeasureText(runText, out var bounds);
            ascent = Math.Max(ascent, -bounds.Top);
            descent = Math.Max(descent, bounds.Bottom);
        }

        var baselineY = rect.Y + (rect.H - (ascent + descent)) * 0.5f + ascent;
        var cursor = x;

        foreach (var (runText, runFont) in runs)
        {
            gui.CurrentNode.DrawList.Add(new Text(runText, new Vector2(cursor, baselineY), runFont.SkFont, paint));
            runFont.SkFont.MeasureText(runText, out var bounds);
            cursor += bounds.Width;
        }

        return cursor + CrumbIconGap;
    }

    static void DrawBreadcrumbUnderline(Gui gui, Color color)
    {
        var rect = gui.CurrentNode.Rect;
        gui.DrawRect(new Rect(rect.X + (CrumbPadding * 0.5f), rect.Y + rect.H - 3.5f,
            rect.W - CrumbPadding, 1.5f), color);
    }

    /// <summary>
    /// The chevron between crumbs. When the crumb to its left carries children it is clickable: a
    /// hover highlights it and a click opens a context menu listing those children under the bar.
    /// </summary>
    static void RenderBreadcrumbSeparator(Gui gui, BreadcrumbItem leftItem, int index, SKFont font,
        float height, Color color, Color linkColor)
    {
        var hasChildren = leftItem.Children is { Count: > 0 };
        var state = gui.ControlState($"breadcrumb/menu/{index}", () => new BreadcrumbMenuState());

        var clicked = false;
        var hovered = false;

        using (gui.Node(MeasureTextWidth(font, Chevron), height, $"breadcrumb/sep/{index}")
                   .Margin(2, 0).AlignContent(0.5f, 0.5f).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && hasChildren)
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

            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                state.Anchor = new Vector2(rect.X, rect.Y + rect.H);
                gui.DrawText(Chevron, font.Size,
                    !hasChildren ? color : hovered || state.IsOpen ? linkColor : color,
                    centerInRect: true);
            }

            if (clicked) state.IsOpen = !state.IsOpen;
        }

        if (!hasChildren) return;

        // Menu opening is deferred by one frame: the press that opened it must not be read on that
        // same frame as a click outside the menu, which would dismiss it before it ever shows.
        var isOpen = state.IsOpen && !clicked;
        gui.ContextMenu(ref isOpen, builder =>
        {
            foreach (var child in leftItem.Children!)
                builder.Item(child.Label, () => child.OnClick?.Invoke());
        }, position: state.Anchor);
        if (!clicked) state.IsOpen = isOpen;
    }
}
