using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Serilog;

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
    /// Gets the list of example projects to build
    /// </summary>
    private List<Project> ExampleProjects => [.. Solution.AllProjects
        .Where(p => p.Directory.ToString().Contains("/Examples/") &&
                   p.GetProperty("ExcludeFromBuild")?.Equals("true", StringComparison.OrdinalIgnoreCase) != true)];

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
                    .SetProjectFile(project)
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
                    if (project.GetProperty("OutputType")?.Equals("Library", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Log.Information("Skipping publish for library project: {Project}", project.Name);
                        continue;
                    }
                    var projectOutput = runtimeOutput / project.Name;

                    Log.Information("Publishing {Project} for {Runtime}", project.Name, runtime);

                    _ = DotNetTasks.DotNetPublish(s => s
                        .SetProject(project)
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
            .Select(project => $"- **{project.Name}**: {GetExampleDescription(project.Name)}")
            .ToArray();

        return string.Join(Environment.NewLine, descriptions);
    }

    /// <summary>
    /// Gets a description for a example project based on its name
    /// </summary>
    private static string GetExampleDescription(string projectName) =>
        projectName switch
        {
            "Example-01" => "Basic Guinevere usage example",
            "Example-01-OpenGL-OpenTK" => "OpenGL rendering with OpenTK integration",
            "Example-01-OpenGL-Raylib" => "OpenGL rendering with Raylib integration",
            "Example-01-OpenGL-SilkNet" => "OpenGL rendering with Silk.NET integration",
            "Example-01-Vulkan-SilkNet" => "Vulkan rendering with Silk.NET integration",
            "Example-02-SimpleLayout" => "Demonstrates simple layout system",
            "Example-02-Layout" => "Shows nested layout with children",
            "Example-03-Texts" => "Text rendering and typography examples",
            "Example-05-SingleNodeExpandMargin" => "Layout margin and expansion demo",
            "Example-41-AdvancedLayoutDemo" => "Advanced layout system features",
            "Example-10-ResponsiveLayout" => "Responsive design examples",
            "Example-43-AnimatedLayoutDemo" => "Layout animations and transitions",
            "Example-50-Excalibur-Controls" => "Basic UI controls demonstration",
            "Example-75-PaperUI-Dashboard" => "Material Design style dashboard",
            "Example-70-PanGui-HelloWorld" => "Pan GUI integration - Hello World",
            "Example-71-PanGui-HelloTriangle" => "Pan GUI integration - Triangle rendering",
            "Example-72-PanGui-AirbnbSlider" => "Pan GUI integration - Airbnb style slider",
            "Example-73-PanGui-MusicApp" => "Pan GUI integration - Music player UI",
            "Example-74-PanGui-Heart" => "Pan GUI integration - Heart animation",
            _ => "Example application demonstrating Guinevere features"
        };
}
