namespace Guinevere;

public partial class Gui
{
    /// <summary>
    /// The user's system appearance as of this frame's <see cref="BeginFrame"/>, so both passes agree; no preference
    /// when the platform publishes no <see cref="IAppearanceCapability"/>.
    /// </summary>
    public SystemAppearance SystemAppearance { get; private set; }

    /// <summary>Whether <see cref="SystemAppearance"/> differs from the previous frame's value.</summary>
    public bool SystemAppearanceChanged { get; private set; }

    void UpdateSystemAppearance()
    {
        var next = Platform.TryGet<IAppearanceCapability>(out var appearance)
            ? appearance!.Appearance
            : SystemAppearance.NoPreference;
        SystemAppearanceChanged = next != SystemAppearance;
        SystemAppearance = next;
    }
}
