using MASS4.Attributes;

namespace Example_11_Excalibur_Attributes;

// [Title] draws a heading, optional subtitle and line above a member.
public sealed class TitleDemo
{
    [Title("Audio", "How the game sounds")] public float Volume { get; set; } = 0.8f;

    public bool Subtitles { get; set; } = true;

    [Title("Controls", bold: false, horizontalLine: false)] public float Sensitivity { get; set; } = 1;
}
