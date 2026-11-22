using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [Title] draws a heading, optional subtitle and line above a member.
/// <summary>Demonstrates headings and subtitles in the generated form.</summary>
public sealed class TitleDemo
{
    /// <summary>Volume grouped under the audio heading.</summary>
    [Title("Audio", "How the game sounds")] public float Volume { get; set; } = 0.8f;

    /// <summary>Whether subtitles are enabled.</summary>
    public bool Subtitles { get; set; } = true;

    /// <summary>Control sensitivity grouped under a plain controls heading.</summary>
    [Title("Controls", bold: false, horizontalLine: false)] public float Sensitivity { get; set; } = 1;
}
