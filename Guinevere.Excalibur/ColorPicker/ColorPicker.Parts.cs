using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed class ColorDragState
    {
        public bool Dragging;
    }

    static readonly string[] VerticalModifier = ["vertical"];

    /// <summary>
    /// The two-dimensional part of a color picker: drag or click to pick, arrows nudge the cursor once focused. Styled
    /// by <c>color-area</c> (<c>:disabled</c>, <c>:focus</c>) and its drawn <c>cursor</c> ring (<c>:focus</c>).
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The edited color.</param>
    /// <param name="shape">The surface; <see cref="ColorPickerShapes.Rectangle"/> by default.</param>
    /// <param name="width">Node width; 0 expands.</param>
    /// <param name="height">Node height; 0 expands.</param>
    /// <param name="enabled">Whether the area responds to input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for stylesheet selectors and persistent control state.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the color changed this frame.</returns>
    public static bool ColorArea(this Gui gui, ref Hsva value, IColorPickerShape? shape = null,
        float width = 200, float height = 150, bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        shape ??= ColorPickerShapes.Rectangle;
        ExcaliburStyles.Ensure(gui);
        var node = Sized(gui.StyledNode("color-area", classes, id, disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber), width, height);
        var before = value;
        using (node.Enter())
        {
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            var rect = node.InnerRect;
            var drag = gui.ControlState(node.Id, () => new ColorDragState());
            if (enabled)
            {
                value = EditArea(gui, drag, shape, rect, value);
            }
            else
            {
                drag.Dragging = false;
            }

            shape.Draw(gui, rect, value);
            DrawPartCentered(gui, "cursor", shape.Cursor(rect, value), 12f);
        }
        return value != before;
    }

    /// <summary>
    /// A one-channel strip, such as the hue or alpha bar beside a color area: drag or click to set the channel, arrows
    /// step it by 1% once focused. Styled by <c>color-strip</c> (<c>:vertical</c>, <c>:disabled</c>) and its drawn
    /// <c>thumb</c> (<c>width</c> is its thickness across a horizontal strip, <c>height</c> across a vertical one).
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The edited color.</param>
    /// <param name="channel">The channel the strip edits.</param>
    /// <param name="vertical">Whether the strip runs bottom (0) to top (max) instead of left to right.</param>
    /// <param name="length">Length along the strip; 0 expands.</param>
    /// <param name="thickness">Size across the strip.</param>
    /// <param name="enabled">Whether the strip responds to input.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for stylesheet selectors and persistent control state.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the color changed this frame.</returns>
    public static bool ColorStrip(this Gui gui, ref Hsva value, HsvChannel channel, bool vertical = false,
        float length = ControlMetrics.FieldWidth, float thickness = 14, bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ExcaliburStyles.Ensure(gui);
        var node = gui.StyledNode("color-strip", classes, id, modifiers: vertical ? VerticalModifier : null,
            disabled: !enabled, filePath: filePath, lineNumber: lineNumber);
        Sized(node, vertical ? thickness : length, vertical ? length : thickness);
        var before = value;
        using (node.Enter())
        {
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            var rect = node.InnerRect;
            var drag = gui.ControlState(node.Id, () => new ColorDragState());
            if (enabled)
            {
                value = EditStrip(gui, drag, rect, value, channel, vertical);
            }
            else
            {
                drag.Dragging = false;
            }

            DrawChannelGradient(gui, rect, value, channel, vertical);
            DrawStripThumb(gui, rect, ChannelFraction(value, channel), vertical);
        }
        return value != before;
    }

    /// <summary>
    /// A wrapping row of color swatches; clicking one, or Space/Enter on a focused one, picks it. Each is a
    /// <c>swatch</c> node (<c>:selected</c> when it is the current color) inside a <c>swatches</c> row.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The edited color.</param>
    /// <param name="colors">The swatch colors.</param>
    /// <param name="size">Side of each swatch.</param>
    /// <param name="enabled">Whether the swatches respond to input.</param>
    /// <param name="classes">Extra classes for the row.</param>
    /// <param name="id">Element id of the row; swatch ids derive from it.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether a swatch was picked this frame.</returns>
    public static bool ColorSwatches(this Gui gui, ref Hsva value, IReadOnlyList<Color> colors, float size = 18,
        bool enabled = true, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(colors);
        ExcaliburStyles.Ensure(gui);
        var picked = false;
        var current = value.ToColor();
        using (gui.StyledNode("swatches", classes, id, disabled: !enabled, filePath: filePath, lineNumber: lineNumber)
                   .ExpandWidth().Enter())
        {
            for (var i = 0; i < colors.Count; i++)
            {
                var color = colors[i];
                using (gui.StyledNode("swatch", modifiers: color == current ? SelectedModifier : null,
                           disabled: !enabled).Width(size).Height(size).Enter())
                {
                    if (gui.Pass != Pass.Pass2Render)
                    {
                        continue;
                    }

                    DrawColorSample(gui, gui.CurrentNode.InnerRect, color);
                    if (!enabled)
                    {
                        continue;
                    }

                    gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                    if (!Activated(gui))
                    {
                        continue;
                    }

                    value = Hsva.FromColor(color, value.H, value.S);
                    picked = true;
                }
            }
        }
        return picked;
    }

    /// <summary>
    /// The before/after sample of a picker. With <paramref name="before"/>, the left half shows it and clicking that
    /// half asks to restore it. Styled by <c>color-preview</c>; translucent colors show over the <c>checker</c> part.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="after">The current color.</param>
    /// <param name="before">The color before editing, or <c>null</c> to show only the current one.</param>
    /// <param name="width">Node width; 0 expands.</param>
    /// <param name="height">Node height; 0 expands.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Element id for stylesheet selectors.</param>
    /// <param name="filePath">Captured by the compiler; makes this call site a unique control.</param>
    /// <param name="lineNumber">Captured by the compiler; makes this call site a unique control.</param>
    /// <returns>Whether the "before" half was clicked this frame.</returns>
    public static bool ColorPreview(this Gui gui, Color after, Color? before = null, float width = 48,
        float height = ControlMetrics.CompactHeight, IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ExcaliburStyles.Ensure(gui);
        var node = Sized(gui.StyledNode("color-preview", classes, id, filePath: filePath, lineNumber: lineNumber),
            width, height);
        using (node.Enter())
        {
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            var rect = node.InnerRect;
            if (before is not { } old)
            {
                DrawColorSample(gui, rect, after);
                return false;
            }
            var half = new Rect(rect.X, rect.Y, rect.W / 2f, rect.H);
            DrawColorSample(gui, half, old);
            DrawColorSample(gui, new Rect(rect.X + half.W, rect.Y, rect.W - half.W, rect.H), after);
            return gui.GetInteractable().OnClick() && gui.IsPointerIn(half);
        }
    }

    /// <summary>Draws a color over the checkerboard when it is translucent.</summary>
    static void DrawColorSample(Gui gui, Rect rect, Color color)
    {
        if (color.A < 255)
        {
            ColorPaint.FillChecker(gui, rect);
        }

        gui.DrawRect(rect, color);
    }

    /// <summary>Registers the area for focus and applies this frame's drag and arrow keys.</summary>
    static Hsva EditArea(Gui gui, ColorDragState drag, IColorPickerShape shape, Rect rect, Hsva value)
    {
        gui.RegisterFocusable(claimsArrowKeys: true);
        if (Dragged(gui, drag))
        {
            value = shape.Pick(rect, gui.LocalMousePosition, value);
        }

        if (ArrowDirection(gui) is not { } direction)
        {
            return value;
        }

        return shape.Pick(rect, shape.Cursor(rect, value) + direction * new Vector2(rect.W, rect.H) / 100f, value);
    }

    /// <summary>Registers the strip for focus and applies this frame's drag and arrow keys.</summary>
    static Hsva EditStrip(Gui gui, ColorDragState drag, Rect rect, Hsva value, HsvChannel channel, bool vertical)
    {
        gui.RegisterFocusable(claimsArrowKeys: true);
        if (Dragged(gui, drag))
        {
            value = WithChannel(value, channel, StripFraction(rect, gui.LocalMousePosition, vertical));
        }

        if (ArrowDirection(gui) is not { } direction)
        {
            return value;
        }

        var step = (vertical ? -direction.Y : direction.X) / 100f;
        return WithChannel(value, channel, ChannelFraction(value, channel) + step);
    }

    /// <summary>Starts a drag on a click and reports whether it is still held.</summary>
    static bool Dragged(Gui gui, ColorDragState drag)
    {
        if (gui.GetInteractable().OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
            drag.Dragging = true;
        }
        if (!gui.Input.IsMouseButtonDown(MouseButton.Left))
        {
            drag.Dragging = false;
        }

        return drag.Dragging;
    }

    /// <summary>The arrow pressed this frame while the current node has focus, as a unit step (down is +Y).</summary>
    static Vector2? ArrowDirection(Gui gui)
    {
        if (!gui.HasFocus())
        {
            return null;
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Left))
        {
            return new Vector2(-1, 0);
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Right))
        {
            return new Vector2(1, 0);
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Up))
        {
            return new Vector2(0, -1);
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Down))
        {
            return new Vector2(0, 1);
        }

        return null;
    }

    static float StripFraction(Rect rect, Vector2 pointer, bool vertical) =>
        vertical
            ? rect.H > 0 ? 1f - (pointer.Y - rect.Y) / rect.H : 0f
            : rect.W > 0 ? (pointer.X - rect.X) / rect.W : 0f;

    /// <summary>A channel's position in [0, 1]; hue maps 0–360 degrees onto it.</summary>
    internal static float ChannelFraction(Hsva value, HsvChannel channel) => channel switch
    {
        HsvChannel.Hue => value.H / 360f,
        HsvChannel.Saturation => value.S,
        HsvChannel.Value => value.V,
        _ => value.A
    };

    /// <summary>Sets one channel from a fraction, clamped; hue stops just short of 360 so it does not wrap to red.</summary>
    internal static Hsva WithChannel(Hsva value, HsvChannel channel, float fraction)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        return channel switch
        {
            HsvChannel.Hue => value with { H = Math.Min(fraction * 360f, 359.9f) },
            HsvChannel.Saturation => value with { S = fraction },
            HsvChannel.Value => value with { V = fraction },
            _ => value with { A = fraction }
        };
    }

    static void DrawChannelGradient(Gui gui, Rect rect, Hsva value, HsvChannel channel, bool vertical)
    {
        switch (channel)
        {
            case HsvChannel.Hue:
                ColorPaint.FillGradient(gui, rect, ColorPaint.HueStops, vertical);
                break;
            case HsvChannel.Saturation:
                ColorPaint.FillGradient(gui, rect, (value with { S = 0f, A = 1f }).ToColor(),
                    (value with { S = 1f, A = 1f }).ToColor(), vertical);
                break;
            case HsvChannel.Value:
                ColorPaint.FillGradient(gui, rect, (value with { V = 0f, A = 1f }).ToColor(),
                    (value with { V = 1f, A = 1f }).ToColor(), vertical);
                break;
            default:
                ColorPaint.FillChecker(gui, rect);
                ColorPaint.FillGradient(gui, rect, (value with { A = 0f }).ToColor(), (value with { A = 1f }).ToColor(),
                    vertical);
                break;
        }
    }

    static void DrawStripThumb(Gui gui, Rect rect, float fraction, bool vertical)
    {
        var style = gui.ResolvePart("thumb", gui.HasFocus() ? StyleState.Focus : StyleState.None,
            vertical ? VerticalModifier : null);
        fraction = Math.Clamp(fraction, 0f, 1f);
        if (vertical)
        {
            var size = PartLength(style, "height", rect.H, 6f);
            var y = rect.Y + (1f - fraction) * rect.H;
            gui.DrawStyledBox(style, new Rect(rect.X - 2f, y - size / 2f, rect.W + 4f, size));
            return;
        }
        var width = PartLength(style, "width", rect.W, 6f);
        var x = rect.X + fraction * rect.W;
        gui.DrawStyledBox(style, new Rect(x - width / 2f, rect.Y - 2f, width, rect.H + 4f));
    }

    /// <summary>Draws a styled part of the given <c>width</c>/<c>height</c> centered on a point.</summary>
    static void DrawPartCentered(Gui gui, string part, Vector2 center, float fallbackSize)
    {
        var style = gui.ResolvePart(part, gui.HasFocus() ? StyleState.Focus : StyleState.None);
        var width = PartLength(style, "width", fallbackSize, fallbackSize);
        var height = PartLength(style, "height", fallbackSize, fallbackSize);
        gui.DrawStyledBox(style, new Rect(center.X - width / 2f, center.Y - height / 2f, width, height));
    }
}
