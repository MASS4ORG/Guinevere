namespace Guinevere;

/// <summary>
/// The screen corner a toast notification is pinned to.
/// </summary>
public enum ToastCorner
{
    /// <summary>Top-left corner, stacking downward.</summary>
    TopLeft,

    /// <summary>Top-right corner, stacking downward.</summary>
    TopRight,

    /// <summary>Bottom-left corner, stacking upward.</summary>
    BottomLeft,

    /// <summary>Bottom-right corner, stacking upward.</summary>
    BottomRight,
}

/// <summary>
/// Options that control how a toast notification is displayed.
/// </summary>
public sealed record ToastOptions
{
    /// <summary>How long the toast stays on screen, in seconds.</summary>
    public float Duration { get; init; } = 3f;

    /// <summary>The corner the toast is pinned to.</summary>
    public ToastCorner Corner { get; init; } = ToastCorner.BottomRight;

    /// <summary>Distance from the screen edge in pixels.</summary>
    public float Margin { get; init; } = 16f;

    /// <summary>Inner padding around the text in pixels.</summary>
    public float Padding { get; init; } = 12f;

    /// <summary>Maximum toast width in pixels. Single-line text is never wrapped.</summary>
    public float MaxWidth { get; init; } = 320f;

    /// <summary>Text size in pixels.</summary>
    public float FontSize { get; init; } = 13f;

    /// <summary>Corner radius of the toast panel.</summary>
    public float BorderRadius { get; init; } = 6f;

    /// <summary>Fade-in duration in seconds while the toast appears.</summary>
    public float FadeInSeconds { get; init; } = 0.15f;

    /// <summary>Fade-out duration in seconds while the toast expires.</summary>
    public float FadeOutSeconds { get; init; } = 0.35f;

    /// <summary>Clicking a toast dismisses it immediately.</summary>
    public bool DismissOnClick { get; init; } = true;

    /// <summary>Toast background fill color.</summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>Toast border color.</summary>
    public Color? BorderColor { get; init; }

    /// <summary>Toast text color.</summary>
    public Color? TextColor { get; init; }

    /// <summary>Optional accent strip color drawn along the leading edge.</summary>
    public Color? AccentColor { get; init; }
}

public static partial class ControlsExtensions
{
    private sealed class ToastState
    {
        public List<ToastEntry> Entries { get; } = new();

        /// <summary>The visible set captured during Pass1, so both passes of the frame render the same toasts.</summary>
        public List<ToastEntry> Visible { get; } = new();
    }

    private sealed record ToastEntry(string Text, float ExpiresAt, float SpawnedAt, ToastOptions Options);

    private const string ToastStateId = "Guinevere.Toast";
    private const float ToastSpacing = 8f;

    /// <summary>Above menu bars and scroll bars, below the drag ghost.</summary>
    private const int ToastZIndex = 9000;

    /// <summary>
    /// Queues a toast notification to be shown by the next <see cref="Toasts"/> call. Every call adds
    /// an entry, so repeated calls stack the same message in its corner with its own lifetime. This
    /// method only records the message; enqueue and render at different call sites so both passes of
    /// a frame agree on the visible set.
    /// </summary>
    public static void Toast(this Gui gui, string text, ToastOptions? options = null)
    {
        var state = gui.ControlState(ToastStateId, () => new ToastState());
        var opts = options ?? new ToastOptions();
        var now = gui.Time.Elapsed;

        state.Entries.Add(new ToastEntry(text, now + opts.Duration, now, opts));
    }

    /// <summary>
    /// Renders the toast notifications queued via <see cref="Toast"/>. Call once per frame like any
    /// other widget. Expired toasts are pruned automatically.
    /// </summary>
    public static void Toasts(this Gui gui)
    {
        var state = gui.ControlState(ToastStateId, () => new ToastState());
        var now = gui.Time.Elapsed;

        if (gui.Pass == Pass.Pass1Build)
        {
            state.Entries.RemoveAll(e => e.ExpiresAt <= now);
            state.Visible.Clear();
            state.Visible.AddRange(state.Entries.Where(e => e.ExpiresAt > now));
        }

        RenderToasts(gui, state, now);
    }

    /// <summary>
    /// Removes all active and pending toast notifications.
    /// </summary>
    public static void ClearToasts(this Gui gui)
    {
        gui.ControlState(ToastStateId, () => new ToastState()).Entries.Clear();
    }

    private static void RenderToasts(Gui gui, ToastState state, float now)
    {
        if (state.Visible.Count == 0) return;

        var usedHeight = new Dictionary<ToastCorner, float>();
        var screen = gui.ScreenRect;
        var toRemove = new List<ToastEntry>();

        foreach (var entry in state.Visible)
        {
            var opts = entry.Options;
            var font = new SKFont { Size = opts.FontSize };
            font.MeasureText(entry.Text, out var textBounds);

            var width = Math.Min(textBounds.Width + opts.Padding * 2, opts.MaxWidth);
            var height = textBounds.Height + opts.Padding * 2;
            var cornerOffset = usedHeight.GetValueOrDefault(opts.Corner);
            var position = ToastPosition(screen, opts, width, height, cornerOffset);
            usedHeight[opts.Corner] = cornerOffset + height + ToastSpacing;

            var alpha = ToastAlpha(now, entry, opts);

            using (gui.Node(width, height).AbsoluteScreen(position.X, position.Y).Enter())
            {
                gui.SetZIndex(ToastZIndex);

                if (gui.Pass == Pass.Pass2Render && alpha > 0)
                {
                    var bg = WithAlpha(opts.BackgroundColor ?? Color.FromArgb(255, 255, 255, 255), alpha);
                    var border = WithAlpha(opts.BorderColor ?? Color.FromArgb(255, 200, 200, 200), alpha);

                    gui.DrawBackgroundRect(bg, opts.BorderRadius);
                    gui.DrawRectBorder(gui.CurrentNode.Rect, border, 1f, opts.BorderRadius);

                    if (opts.AccentColor is { } accent)
                        gui.DrawRect(new Rect(position.X, position.Y, 4, height), WithAlpha(accent, alpha));

                    if (opts.DismissOnClick && gui.Input.IsMouseButtonPressed(MouseButton.Left) &&
                        IsMouseInRect(gui.Input.MousePosition, gui.CurrentNode.Rect))
                        toRemove.Add(entry);
                }

                // Built in both passes so the text node is measured during Pass1 layout; creating it
                // only in Pass2 leaves its rect (0,0,0,0) and the glyphs draw at the canvas origin.
                gui.DrawText(entry.Text, opts.FontSize, WithAlpha(opts.TextColor ?? Color.Black, alpha),
                    centerInRect: false);
            }
        }

        if (toRemove.Count > 0)
            foreach (var entry in toRemove)
                state.Entries.Remove(entry);
    }

    private static Vector2 ToastPosition(Rect screen, ToastOptions opts, float width, float height, float above)
    {
        var x = opts.Corner switch
        {
            ToastCorner.TopLeft or ToastCorner.BottomLeft => opts.Margin,
            _ => screen.W - opts.Margin - width,
        };

        var y = opts.Corner switch
        {
            ToastCorner.TopLeft or ToastCorner.TopRight => opts.Margin + above,
            _ => screen.H - opts.Margin - height - above,
        };

        return new Vector2(x, y);
    }

    private static int ToastAlpha(float now, ToastEntry entry, ToastOptions opts)
    {
        var age = now - entry.SpawnedAt;
        var remaining = entry.ExpiresAt - now;

        var alpha = 1f;
        if (age < opts.FadeInSeconds) alpha = age / opts.FadeInSeconds;
        if (remaining < opts.FadeOutSeconds) alpha = Math.Min(alpha, remaining / opts.FadeOutSeconds);

        return Math.Max(0, Math.Min(255, (int)(alpha * 255)));
    }

    private static Color WithAlpha(Color color, int alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);
}