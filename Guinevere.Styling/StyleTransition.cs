using System.Globalization;

namespace Guinevere;

/// <summary>
/// A modifier's transition annotation, such as <c>:hover(0.15 ease-out)</c>: how long the properties its rule sets
/// take to reach their new values, and the easing on the way.
/// </summary>
/// <param name="Duration">Seconds per transition.</param>
/// <param name="EasingName">The easing as written; <c>ease</c> when omitted.</param>
public sealed record StyleTransition(float Duration, string EasingName)
{
    /// <summary>The easing function named by <see cref="EasingName"/>.</summary>
    public Func<float, float> Easing { get; } = Guinevere.Easing.TryParse(EasingName, out var easing)
        ? easing
        : throw new FormatException($"Unknown easing '{EasingName}'");

    /// <summary>
    /// Parses an annotation body: a duration in seconds (<c>0.15</c>, <c>0.15s</c>) or milliseconds (<c>150ms</c>),
    /// then an optional easing accepted by <see cref="Easing.TryParse"/>.
    /// </summary>
    /// <param name="text">The text between the parentheses.</param>
    /// <exception cref="FormatException">The duration or easing is malformed.</exception>
    public static StyleTransition Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var body = text.Trim();
        var split = body.IndexOfAny([' ', '\t', '\r', '\n']);
        var duration = split < 0 ? body : body[..split];
        var easing = split < 0 ? "ease" : body[split..].Trim();
        return new StyleTransition(Seconds(duration), easing);
    }

    static float Seconds(string text)
    {
        var (number, scale) = text.EndsWith("ms", StringComparison.OrdinalIgnoreCase) ? (text[..^2], 0.001f)
            : text.EndsWith('s') ? (text[..^1], 1f)
            : (text, 1f);
        return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0f
            ? value * scale
            : throw new FormatException($"Malformed transition duration '{text}'");
    }
}
