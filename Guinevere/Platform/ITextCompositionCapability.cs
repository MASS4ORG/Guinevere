namespace Guinevere;

/// <summary>
/// Optional input-method (IME) composition. While the user composes text, such as Japanese or Chinese, the platform
/// reports the uncommitted text here; committed text still arrives through
/// <see cref="IInputHandler.GetTypedCharacters"/>. A focused text control draws the composition at its caret and
/// reports where that caret is so the platform can place its candidate window.
/// </summary>
public interface ITextCompositionCapability : IPlatformCapability
{
    /// <summary>The text being composed, or empty when no composition is active.</summary>
    string CompositionText { get; }

    /// <summary>Caret offset inside <see cref="CompositionText"/>.</summary>
    int CompositionCaret { get; }

    /// <summary>Tells the platform the caret rectangle in window coordinates, for its candidate window.</summary>
    void SetCompositionArea(Rect caret);
}
