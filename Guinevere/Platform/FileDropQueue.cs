using System.Collections.Concurrent;

namespace Guinevere;

/// <summary>Copies native file-drop paths into a queue safe to consume on the GUI thread.</summary>
public sealed class FileDropQueue : IFileDropCapability
{
    readonly ConcurrentQueue<FileDrop> pending = new();

    /// <summary>Queues a completed desktop drop, retaining the supplied window position.</summary>
    public void Enqueue(IEnumerable<string> paths, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var copied = paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        if (copied.Length > 0) pending.Enqueue(new FileDrop(position, Array.AsReadOnly(copied)));
    }

    /// <inheritdoc />
    public bool TryDequeue(out FileDrop drop) => pending.TryDequeue(out drop);
}
