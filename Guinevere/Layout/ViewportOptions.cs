namespace Guinevere;

/// <summary>Insets in the viewport's parent coordinates, such as cutouts, overscan or system bars.</summary>
public readonly record struct ViewportInsets(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Removes insets without producing negative content dimensions.</summary>
    public Rect Apply(Rect bounds)
    {
        Validate(Left);
        Validate(Top);
        Validate(Right);
        Validate(Bottom);
        var left = Math.Min(Left, bounds.W);
        var top = Math.Min(Top, bounds.H);
        return new Rect(bounds.X + left, bounds.Y + top,
            Math.Max(0, bounds.W - left - Right), Math.Max(0, bounds.H - top - Bottom));
    }

    static void Validate(float value)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}

/// <summary>Uniform scale policies for a reference resolution.</summary>
public enum ReferenceScaleMode
{
    /// <summary>Fits both reference dimensions inside the safe bounds.</summary>
    Fit,
    /// <summary>Scales until both safe dimensions are covered.</summary>
    Fill,
    /// <summary>Matches the reference width.</summary>
    MatchWidth,
    /// <summary>Matches the reference height.</summary>
    MatchHeight,
    /// <summary>Uses the supplied display density.</summary>
    Constant
}

/// <summary>Controls whether extra space expands the layout or surrounds a fixed reference canvas.</summary>
public enum ViewportAspectPolicy
{
    /// <summary>Uses all safe space and keeps anchors reachable across aspect ratios.</summary>
    Expand,
    /// <summary>Centers the reference canvas, with letterboxing or cropping as needed.</summary>
    PreserveReference
}

/// <summary>Immutable viewport settings owned by the application; scales can change between frames.</summary>
public sealed record ViewportOptions
{
    /// <summary>Viewport rectangle in the enclosing viewport's logical coordinates.</summary>
    public required Rect Bounds { get; init; }
    /// <summary>Excluded edges in the same coordinates as Bounds.</summary>
    public ViewportInsets Insets { get; init; }
    /// <summary>Logical design size used by the reference scaling policy.</summary>
    public Vector2 ReferenceResolution { get; init; } = new(1920, 1080);
    /// <summary>Reference scaling policy.</summary>
    public ReferenceScaleMode ScaleMode { get; init; } = ReferenceScaleMode.Fit;
    /// <summary>Policy for space beyond the reference aspect ratio.</summary>
    public ViewportAspectPolicy AspectPolicy { get; init; }
    /// <summary>Lower limit of the combined reference and user scale.</summary>
    public float MinScale { get; init; } = 0.25f;
    /// <summary>Upper limit of the combined reference and user scale.</summary>
    public float MaxScale { get; init; } = 4f;
    /// <summary>User scale for the entire UI.</summary>
    public float UiScale { get; init; } = 1;
    /// <summary>Additional text scale, independent of geometry.</summary>
    public float TextScale { get; init; } = 1;
    /// <summary>Display density for Constant mode; reference modes already scale to the viewport dimensions.</summary>
    public float DisplayScale { get; init; } = 1;

    /// <summary>Resolves deterministic logical bounds and a uniform transform for this viewport.</summary>
    public ViewportLayout Resolve()
    {
        ValidateBounds();
        Positive(ReferenceResolution.X);
        Positive(ReferenceResolution.Y);
        Positive(MinScale);
        Positive(MaxScale);
        Positive(UiScale);
        Positive(TextScale);
        Positive(DisplayScale);
        if (MaxScale < MinScale) throw new ArgumentOutOfRangeException(nameof(MaxScale));
        if (!Enum.IsDefined(AspectPolicy)) throw new ArgumentOutOfRangeException(nameof(AspectPolicy));
        var safe = Insets.Apply(Bounds);
        var widthScale = safe.W / ReferenceResolution.X;
        var heightScale = safe.H / ReferenceResolution.Y;
        var referenceScale = ScaleMode switch
        {
            ReferenceScaleMode.Fit => Math.Min(widthScale, heightScale),
            ReferenceScaleMode.Fill => Math.Max(widthScale, heightScale),
            ReferenceScaleMode.MatchWidth => widthScale,
            ReferenceScaleMode.MatchHeight => heightScale,
            ReferenceScaleMode.Constant => DisplayScale,
            _ => throw new ArgumentOutOfRangeException(nameof(ScaleMode))
        };
        var scale = Math.Clamp(referenceScale * UiScale, MinScale, MaxScale);
        var size = AspectPolicy == ViewportAspectPolicy.Expand
            ? new Vector2(safe.W / scale, safe.H / scale) : ReferenceResolution;
        var offset = new Vector2(safe.X, safe.Y)
            + (new Vector2(safe.W, safe.H) - size * scale) * 0.5f;
        return new ViewportLayout(safe, new Rect(0, 0, size.X, size.Y), scale, TextScale,
            Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offset));
    }

    void ValidateBounds()
    {
        if (!float.IsFinite(Bounds.X) || !float.IsFinite(Bounds.Y)
            || !float.IsFinite(Bounds.W) || !float.IsFinite(Bounds.H) || Bounds.W < 0 || Bounds.H < 0)
            throw new ArgumentOutOfRangeException(nameof(Bounds));
    }

    static void Positive(float value)
    {
        if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
