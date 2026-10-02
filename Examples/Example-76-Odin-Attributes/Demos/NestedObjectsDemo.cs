namespace Example_76_Odin_Attributes;

public sealed class AudioSettings
{
    public float Volume { get; set; } = 0.8f;
    public bool Muted { get; set; }
    public MixerSettings Mixer { get; set; } = new();
}

public sealed class MixerSettings
{
    public float Music { get; set; } = 0.6f;
    public float Effects { get; set; } = 1;
}

// Plain classes open as foldable compartments; boxes inside boxes alternate their shade.
public sealed class NestedObjectsDemo
{
    public string Profile { get; set; } = "Default";
    public AudioSettings Audio { get; set; } = new();
}
