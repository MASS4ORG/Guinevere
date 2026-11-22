namespace Example11ExcaliburAttributes;

/// <summary>Audio playback settings shown in the nested object editor.</summary>
public sealed class AudioSettings
{
    /// <summary>Master playback volume.</summary>
    public float Volume { get; set; } = 0.8f;
    /// <summary>Whether audio playback is muted.</summary>
    public bool Muted { get; set; }
    /// <summary>Per-channel mixer settings.</summary>
    public MixerSettings Mixer { get; set; } = new();
}

/// <summary>Individual music and effects channel levels.</summary>
public sealed class MixerSettings
{
    /// <summary>Music channel volume.</summary>
    public float Music { get; set; } = 0.6f;
    /// <summary>Sound effects channel volume.</summary>
    public float Effects { get; set; } = 1;
}

// Plain classes open as foldable compartments; boxes inside boxes alternate their shade.
/// <summary>Demonstrates editing nested reference objects.</summary>
public sealed class NestedObjectsDemo
{
    /// <summary>Profile name shown in the editor.</summary>
    public string Profile { get; set; } = "Default";
    /// <summary>Nested playback configuration.</summary>
    public AudioSettings Audio { get; set; } = new();
}
