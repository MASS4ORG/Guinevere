namespace Guinevere;

/// <summary>Shared, backend-independent validation for an integration's published capabilities.</summary>
public static class PlatformConformance
{
    /// <summary>Validates the required services and every optional service that was published.</summary>
    public static IReadOnlyList<string> Validate(PlatformCapabilities platform, bool requireRenderer = true)
    {
        ArgumentNullException.ThrowIfNull(platform);
        var failures = new List<string>();

        Require<IInputHandler>(platform, failures);
        Require<IClipboard>(platform, failures);
        Require<IWindowHandler>(platform, failures);
        Require<ITimeCapability>(platform, failures);
        if (requireRenderer) Require<ICanvasRenderer>(platform, failures);

        if (platform.TryGet<IInputHandler>(out var input) && input is not null &&
            (!Finite(input.MousePosition) || !Finite(input.MouseDelta) || !float.IsFinite(input.MouseWheelDelta)))
            failures.Add("IInputHandler pointer values must be finite.");

        if (platform.TryGet<IClipboard>(out var clipboard) && clipboard is not null)
        {
            try
            {
                if (clipboard.GetClipboardText() is null)
                    failures.Add("IClipboard.GetClipboardText must not return null.");
            }
            catch (Exception exception)
            {
                failures.Add($"IClipboard read failed: {exception.GetType().Name}.");
            }
        }

        if (platform.TryGet<ITimeCapability>(out var time) && time is not null &&
            (!float.IsFinite(time.DeltaTime) || time.DeltaTime < 0 ||
             !float.IsFinite(time.Elapsed) || time.Elapsed < 0))
            failures.Add("ITimeCapability values must be finite and non-negative.");

        if (platform.TryGet<IDisplayCapability>(out var display) && display is not null)
        {
            if (!float.IsFinite(display.ScaleFactor) || display.ScaleFactor <= 0)
                failures.Add("IDisplayCapability.ScaleFactor must be finite and positive.");
            if (!FiniteNonNegative(display.LogicalSize))
                failures.Add("IDisplayCapability.LogicalSize must be finite and non-negative.");
            if (!FiniteNonNegative(display.FramebufferSize))
                failures.Add("IDisplayCapability.FramebufferSize must be finite and non-negative.");
        }

        if (platform.TryGet<ICursorCapability>(out var cursor) && cursor is not null &&
            !Enum.IsDefined(cursor.Cursor))
            failures.Add("ICursorCapability returned an unknown cursor.");

        if (platform.TryGet<IPointerCapability>(out var pointer) && pointer is not null &&
            pointer is { Locked: true, Visible: true })
            failures.Add("IPointerCapability must hide a locked pointer.");

        return failures;
    }

    static void Require<TCapability>(PlatformCapabilities platform, ICollection<string> failures)
        where TCapability : class, IPlatformCapability
    {
        if (!platform.Supports<TCapability>()) failures.Add($"Missing required {typeof(TCapability).Name}.");
    }

    static bool FiniteNonNegative(Vector2 value) =>
        float.IsFinite(value.X) && value.X >= 0 && float.IsFinite(value.Y) && value.Y >= 0;

    static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
