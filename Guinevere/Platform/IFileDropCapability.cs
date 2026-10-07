namespace Guinevere;

/// <summary>A completed desktop drop at a position in window coordinates.</summary>
/// <param name="Position">The pointer position when the desktop delivered the files.</param>
/// <param name="Paths">The paths supplied by the operating system.</param>
public readonly record struct FileDrop(Vector2 Position, IReadOnlyList<string> Paths);

/// <summary>Optional desktop file drops, queued until the next GUI frame.</summary>
public interface IFileDropCapability : IPlatformCapability
{
    /// <summary>Removes the next completed drop, returning false when none is pending.</summary>
    bool TryDequeue(out FileDrop drop);
}
