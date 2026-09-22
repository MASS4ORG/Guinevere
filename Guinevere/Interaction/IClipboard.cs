namespace Guinevere;

/// <summary>Provides access to the host operating system's text clipboard.</summary>
public interface IClipboard : IPlatformCapability
{
    /// <summary>Gets the current clipboard text, or an empty string when unavailable.</summary>
    string GetClipboardText();

    /// <summary>Replaces the current clipboard text.</summary>
    /// <param name="text">The text to store.</param>
    void SetClipboardText(string text);
}
