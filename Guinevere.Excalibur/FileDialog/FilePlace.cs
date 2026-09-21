namespace Guinevere;

/// <summary>
/// A sidebar shortcut: a labelled directory the dialog can jump to, such as the user's home folder,
/// a drive, or a host-supplied favourite.
/// </summary>
/// <param name="Label">The name shown in the sidebar.</param>
/// <param name="Path">The absolute directory the shortcut opens.</param>
/// <param name="Icon">A glyph drawn before the label.</param>
public sealed record FilePlace(string Label, string Path, string Icon = "📁")
{
    /// <summary>
    /// The places every platform offers: the user's own folders, then each ready drive. Folders the
    /// platform does not define — <c>Downloads</c> has no special-folder id — are resolved by name
    /// under the home directory, and any that does not exist is left out.
    /// </summary>
    /// <returns>The shortcuts, in the order a sidebar should show them.</returns>
    public static IReadOnlyList<FilePlace> Default()
    {
        var places = new List<FilePlace>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Add("Home", home, "🏠");
        Add("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "🖥");
        Add("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "📄");
        // Path is this record's own property, so System.IO's has to be named in full here.
        if (home.Length > 0) Add("Downloads", System.IO.Path.Combine(home, "Downloads"), "⬇");

        foreach (var drive in ReadyDrives())
            Add(drive.Label, drive.Path, drive.Icon);

        return places;

        void Add(string label, string path, string icon)
        {
            if (path.Length == 0 || !Directory.Exists(path)) return;
            if (places.Any(place => string.Equals(place.Path, path, StringComparison.OrdinalIgnoreCase))) return;
            places.Add(new FilePlace(label, path, icon));
        }
    }

    /// <summary>
    /// Filesystems that are mount points rather than places to store anything. Unix reports dozens of
    /// them from <see cref="DriveInfo.GetDrives"/>, and listing them would bury the real drives.
    /// </summary>
    static readonly HashSet<string> pseudoFileSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "autofs", "binfmt_misc", "bpf", "cgroup", "cgroup2", "configfs", "debugfs", "devpts",
        "devtmpfs", "efivarfs", "fuse.gvfsd-fuse", "fusectl", "hugetlbfs", "mqueue", "overlay",
        "proc", "pstore", "ramfs", "securityfs", "squashfs", "sysfs", "tmpfs", "tracefs",
    };

    /// <summary>Every mounted drive that answers, labelled by its volume name where it has one.</summary>
    /// <returns>The drives, or an empty list when they cannot be enumerated.</returns>
    static IEnumerable<FilePlace> ReadyDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var drive in drives)
        {
            string label;
            string path;
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network)) continue;
                if (pseudoFileSystems.Contains(drive.DriveFormat)) continue;

                path = drive.RootDirectory.FullName;
                label = drive.VolumeLabel.Length > 0 ? drive.VolumeLabel : path;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            yield return new FilePlace(label, path, "💾");
        }
    }
}
