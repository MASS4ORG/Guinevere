namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for building example applications.
/// </summary>
partial class Build
{
    [Parameter("Runtime identifiers for examples (default: win-x64,linux-x64)")]
    public readonly string[] ExampleRuntimes = ["win-x64", "linux-x64"];

    [Parameter("Examples output directory (default: ./examples-output)")]
    public readonly AbsolutePath ExamplesOutputDirectory;
    private AbsolutePath ExamplesOutput => ExamplesOutputDirectory ?? RootDirectory / "examples-output";

    /// <summary>
    /// Examples that are not built or published.
    /// </summary>
    private static readonly string[] ExcludedExamples = ["Example-73-PanGui-MusicApp"];

    /// <summary>
    /// Discovers example projects from <c>Examples/*/*.csproj</c>, so a new example needs no registration here
    /// </summary>
    private List<ExampleProject> ExampleProjects => [.. (RootDirectory / "Examples").GlobFiles("*/*.csproj")
        .Select(path => new ExampleProject(path))
        .Where(p => !ExcludedExamples.Contains(p.Name) && p.Property("ExcludeFromBuild") != "true")
        .OrderBy(p => p.Name)];

    /// <summary>An example project file found on disk.</summary>
    private sealed record ExampleProject(AbsolutePath File)
    {
        public string Name => File.NameWithoutExtension;
        public AbsolutePath Directory => File.Parent;

        /// <summary>Reads a literal property from the project file; null when absent.</summary>
        public string Property(string name) =>
            System.Xml.Linq.XDocument.Load(File).Descendants(name).FirstOrDefault()?.Value.Trim();
    }

    /// <summary>
    /// Builds all example applications
    /// </summary>
    private Target BuildExamples => td => td
        .After(Compile)
        .Executes(() =>
        {
            Log.Information("Building {Count} example projects", ExampleProjects.Count);

            foreach (var project in ExampleProjects)
            {
                Log.Information("Building example: {Project}", project.Name);

                _ = DotNetTasks.DotNetBuild(s => s
                    .SetProjectFile(project.File)
                    .SetConfiguration(ConfigurationSet)
                    .EnableNoRestore()
                );
            }

            Log.Information("Successfully built all example projects");
        });

    /// <summary>
    /// Publishes all example applications for multiple platforms
    /// </summary>
    private Target PublishExamples => td => td
        .After(BuildExamples)
        .Produces(ExamplesOutput / "**")
        .Executes(() =>
        {
            ExamplesOutput.CreateDirectory();

            Log.Information("Publishing {Count} example projects for runtimes: {Runtimes}",
                ExampleProjects.Count, string.Join(", ", ExampleRuntimes));

            foreach (var runtime in ExampleRuntimes)
            {
                var runtimeOutput = ExamplesOutput / runtime;
                runtimeOutput.CreateDirectory();

                foreach (var project in ExampleProjects)
                {
                    // Skip publishing if project is a library
                    if (string.Equals(project.Property("OutputType"), "Library", StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Information("Skipping publish for library project: {Project}", project.Name);
                        continue;
                    }
                    var projectOutput = runtimeOutput / project.Name;

                    Log.Information("Publishing {Project} for {Runtime}", project.Name, runtime);

                    _ = DotNetTasks.DotNetPublish(s => s
                        .SetProject(project.File)
                        .SetConfiguration(ConfigurationSet)
                        .SetOutput(projectOutput)
                        .SetRuntime(runtime)
                        .SetSelfContained(true)
                        .SetPublishSingleFile(true)
                        .SetPublishReadyToRun(true)
                        .SetVersion(VersionFull)
                        .SetAssemblyVersion(VersionFull)
                        .SetInformationalVersion(VersionFull)
                    );
                }
            }

            Log.Information("Successfully published all examples to {Directory}", ExamplesOutput);
        });

    /// <summary>
    /// Packages example applications into archives for distribution
    /// </summary>
    private Target PackageExamples => td => td
        .DependsOn(PublishExamples)
        .Produces(ExamplesOutput / "*.zip")
        .Executes(() =>
        {
            foreach (var runtime in ExampleRuntimes)
            {
                var runtimeOutput = ExamplesOutput / runtime;
                var archiveName = $"Guinevere-Examples-{VersionFull}-{runtime}.zip";
                var archivePath = ExamplesOutput / archiveName;

                Log.Information("Creating examples archive: {Archive}", archiveName);

                runtimeOutput.ZipTo(archivePath, compressionLevel: System.IO.Compression.CompressionLevel.Optimal);
            }

            Log.Information("Successfully packaged all examples");
        });

    /// <summary>
    /// Cleans example build outputs
    /// </summary>
    [PublicAPI]
    private Target CleanExamples => td => td
        .Executes(() =>
        {
            ExampleProjects.ForEach(project =>
            {
                (project.Directory / "bin").DeleteDirectory();
                (project.Directory / "obj").DeleteDirectory();
            });

            ExamplesOutput.DeleteDirectory();
            Log.Information("Cleaned example build outputs");
        });

    /// <summary>
    /// Creates a README file for the examples package
    /// </summary>
    [PublicAPI]
    private Target CreateExamplesReadme => td => td
        .Before(PackageExamples)
        .Executes(() =>
        {
            foreach (var runtime in ExampleRuntimes)
            {
                var runtimeOutput = ExamplesOutput / runtime;
                var readmePath = runtimeOutput / "README.md";

                var readmeContent = $@"# Guinevere Examples v{VersionFull}

This package contains example applications demonstrating the Guinevere GUI system.

## Runtime: {runtime}

## Available Examples

{GetExampleDescriptions()}

## Running the Examples

Each example is provided as a self-contained executable:

### Windows ({(runtime.Contains("win") ? "Current Platform" : "Not Current Platform")})
- Double-click the `.exe` files to run the examples
- Or run from command line: `ExampleName.exe`

### Linux ({(runtime.Contains("linux") ? "Current Platform" : "Not Current Platform")})
- Make executable: `chmod +x ExampleName`
- Run from command line: `./ExampleName`

## About Guinevere

Guinevere is a GPU accelerated immediate mode GUI system built on SkiaSharp.
It provides high-performance rendering with modern graphics APIs support.

- **Website**: https://mass4.org
- **Source Code**: https://github.com/brmassa/guinevere
- **Author**: Bruno Massa (massa@brunomassa.com)

## License

MIT License - see the project repository for full license details.
";

                readmePath.WriteAllText(readmeContent);
            }

            Log.Information("Created README files for example packages");
        });

    /// <summary>
    /// Generates descriptions for all example projects
    /// </summary>
    private string GetExampleDescriptions()
    {
        var descriptions = ExampleProjects
            .OrderBy(p => p.Name)
            .Select(project => $"- **{project.Name}**: {GetExampleDescription(project)}")
            .ToArray();

        return string.Join(Environment.NewLine, descriptions);
    }

    /// <summary>
    /// Describes an example with the first prose line of its README. Platform hosts such as
    /// <c>Example-01-OpenGL-SilkNet</c> have no README of their own and borrow the one of <c>Example-01</c>.
    /// </summary>
    private string GetExampleDescription(ExampleProject project)
    {
        const string fallback = "Example application demonstrating Guinevere features";
        var readme = project.Directory / "README.md";
        if (!readme.FileExists())
        {
            var shared = project.Name.Split('-') is [var prefix, var number, ..] ? $"{prefix}-{number}" : null;
            readme = shared is null ? readme : RootDirectory / "Examples" / shared / "README.md";
        }

        return readme.FileExists()
            ? File.ReadLines(readme).Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#')) ?? fallback
            : fallback;
    }
}
