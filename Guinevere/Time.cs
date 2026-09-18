namespace Guinevere;

/// <summary>
/// Provides time measurement and frame rate calculation utilities for game loops.
/// </summary>
public class Time
{
    const float SmoothingInterval = .1f;
    float _smoothingTimer;
    int _framesSinceLastSmooth;

    /// <summary>
    /// Gets the time in seconds that elapsed since the last frame update.
    /// </summary>
    public float DeltaTime { get; private set; }

    /// <summary>
    /// Gets the total time in seconds since the Time instance was created.
    /// </summary>
    public float Elapsed { get; private set; }

    /// <summary>
    /// Gets the total number of frames that have been processed since creation.
    /// </summary>
    public ulong Frames { get; private set; }

    /// <summary>
    /// Gets the instantaneous frames per second (FPS) based on the last frame's delta time.
    /// Returns 0 if delta time is 0 (to avoid division by zero).
    /// </summary>
    public float Fps => DeltaTime > 0 ? 1f / DeltaTime : 0f;

    /// <summary>
    /// Gets the smoothed frames per second (FPS) value, updated every second.
    /// This provides a more stable FPS reading than the instantaneous <see cref="Fps"/> value.
    /// </summary>
    public float SmoothFps { get; private set; }

    /// <summary>
    /// Updates the time measurements. Called once per frame.
    /// </summary>
    public void Update(double deltaTime)
    {
        DeltaTime = (float)deltaTime;
        Elapsed += (float)deltaTime;

        // Update smooth FPS calculation
        Frames++;
        _framesSinceLastSmooth++;
        _smoothingTimer += DeltaTime;
        if (!(_smoothingTimer >= SmoothingInterval)) return;
        SmoothFps = _framesSinceLastSmooth / _smoothingTimer;
        _framesSinceLastSmooth = 0;
        _smoothingTimer = 0f;
    }
}
