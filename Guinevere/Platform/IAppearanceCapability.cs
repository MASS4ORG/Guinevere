namespace Guinevere;

/// <summary>The light or dark appearance the user prefers for applications.</summary>
public enum ColorScheme
{
    /// <summary>The platform has no preference or cannot report one.</summary>
    NoPreference,

    /// <summary>The user prefers dark text on light surfaces.</summary>
    Light,

    /// <summary>The user prefers light text on dark surfaces.</summary>
    Dark
}

/// <summary>Whether the user asked the platform for more contrast.</summary>
public enum ContrastPreference
{
    /// <summary>The platform has no preference or cannot report one.</summary>
    NoPreference,

    /// <summary>The user asked for high contrast.</summary>
    More
}

/// <summary>The user's platform appearance settings; <c>default</c> means no preference for any of them.</summary>
/// <param name="ColorScheme">Preferred light or dark appearance.</param>
/// <param name="AccentColor">The system accent color, or <c>null</c> when the platform has none.</param>
/// <param name="Contrast">Preferred contrast.</param>
public readonly record struct SystemAppearance(
    ColorScheme ColorScheme = ColorScheme.NoPreference,
    Color? AccentColor = null,
    ContrastPreference Contrast = ContrastPreference.NoPreference)
{
    /// <summary>No preference for any setting, as reported by unsupported platforms.</summary>
    public static SystemAppearance NoPreference => default;
}

/// <summary>
/// Optional access to the user's system appearance. <see cref="Gui.SystemAppearance"/> reads it once per frame, so
/// both passes agree; <see cref="Changed"/> serves code outside the frame loop.
/// </summary>
public interface IAppearanceCapability : IPlatformCapability
{
    /// <summary>The latest known appearance; safe to read from any thread.</summary>
    SystemAppearance Appearance { get; }

    /// <summary>Raised, possibly on a background thread, after <see cref="Appearance"/> changes.</summary>
    event Action<SystemAppearance>? Changed;
}
