namespace Guinevere;

/// <summary>Normalizes recognized controllers missing from a backend's gamepad database.</summary>
public static class GamepadMappings
{
    const string XboxSeriesUsbLinux = "030000005e040000120b00000f050000";

    /// <summary>Applies a known raw-device mapping, preserving already mapped and unknown devices.</summary>
    public static GameInputSample Normalize(GameInputSample sample)
    {
        if (sample.IsMapped || sample.MappingId != XboxSeriesUsbLinux) return sample;
        if (sample.Buttons.Length < 11 || sample.Axes.Length < 6) return sample;
        var buttons = new bool[15];
        Array.Copy(sample.Buttons, buttons, 11);
        var hat = sample.Hats.FirstOrDefault();
        buttons[(int)GameButton.Up] = hat.Y < 0;
        buttons[(int)GameButton.Right] = hat.X > 0;
        buttons[(int)GameButton.Down] = hat.Y > 0;
        buttons[(int)GameButton.Left] = hat.X < 0;
        return sample with
        {
            IsMapped = true,
            Buttons = buttons,
            Axes = [sample.Axes[0], sample.Axes[1], sample.Axes[3], sample.Axes[4], sample.Axes[2], sample.Axes[5]]
        };
    }
}
