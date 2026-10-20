namespace Build;

/// <summary>Checks that the shared build files can be copied between MASS4 projects without changes.</summary>
sealed partial class Build
{
    [Parameter("Project directories whose shared NUKE files must match this checkout")]
    readonly string[] SharedBuildDirectories = [];

    static readonly string[] SharedBuildFiles =
    [
        ".editorconfig",
        "Build.Changelog.cs",
        "Build.Check.cs",
        "Build.Compile.cs",
        "Build.NuGet.cs",
        "Build.Pack.cs",
        "Build.Publish.cs",
        "Build.Release.GitHub.cs",
        "Build.Release.GitLab.cs",
        "Build.Release.cs",
        "Build.Solution.cs",
        "Build.Test.cs",
        "Build.Version.cs",
        "Build.cs",
        "Build.csproj",
        "Build.csproj.DotSettings",
        "Configuration.cs",
        "Directory.Build.props",
        "Directory.Build.targets",
        "Globals.cs",
    ];

    /// <summary>Fails when a common NUKE file is missing or differs between the supplied projects.</summary>
    public Target CheckSharedBuild => td => td
        .Executes(() =>
        {
            if (SharedBuildDirectories.Length == 0)
            {
                throw new InvalidOperationException("Supply project directories with --shared-build-directories.");
            }

            var differences = new List<string>();
            var directories = SharedBuildDirectories
                .Prepend(RootDirectory.ToString())
                .Select(directory => Path.GetFullPath(directory, RootDirectory))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var name in SharedBuildFiles)
            {
                var expected = File.ReadAllBytes(BuildProjectDirectory / name);
                foreach (var directory in directories)
                {
                    var path = Path.Combine(directory, ".nuke", name);
                    if (!File.Exists(path) || !expected.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
                    {
                        differences.Add(path);
                    }
                }
            }

            if (differences.Count > 0)
            {
                throw new InvalidOperationException("Shared NUKE files differ or are missing:\n" +
                    string.Join(Environment.NewLine, differences));
            }

            Log.Information("Verified {Files} shared NUKE files across {Projects} projects",
                SharedBuildFiles.Length, directories.Length);
        });
}
