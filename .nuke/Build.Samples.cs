using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace Guinevere.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for building sample applications.
/// </summary>
partial class Build
{
    [Parameter("Runtime identifiers for samples (default: win-x64,linux-x64)")]
    public readonly string[] SampleRuntimes = ["win-x64", "linux-x64"];

    [Parameter("Samples output directory (default: ./samples-output)")]
    public readonly AbsolutePath SamplesOutputDirectory;
    private AbsolutePath SamplesOutput => SamplesOutputDirectory ?? RootDirectory / "samples-output";

    /// <summary>
    /// Gets the list of sample projects to build
    /// </summary>
    private List<Project> SampleProjects => Solution.AllProjects
        .Where(p => p.Directory.ToString().Contains("/Samples/") &&
                   p.GetProperty("ExcludeFromBuild")?.Equals("true", StringComparison.OrdinalIgnoreCase) != true)
        .ToList();

    /// <summary>
    /// Builds all sample applications
    /// </summary>
    private Target BuildSamples => td => td
        .After(Compile)
        .Executes(() =>
        {
            Log.Information("Building {Count} sample projects", SampleProjects.Count);

            foreach (var project in SampleProjects)
            {
                Log.Information("Building sample: {Project}", project.Name);

                _ = DotNetTasks.DotNetBuild(s => s
                    .SetProjectFile(project)
                    .SetConfiguration(ConfigurationSet)
                    .EnableNoRestore()
                );
            }

            Log.Information("Successfully built all sample projects");
        });

    /// <summary>
    /// Publishes all sample applications for multiple platforms
    /// </summary>
    private Target PublishSamples => td => td
        .After(BuildSamples)
        .Produces(SamplesOutput / "**")
        .Executes(() =>
        {
            SamplesOutput.CreateDirectory();

            Log.Information("Publishing {Count} sample projects for runtimes: {Runtimes}",
                SampleProjects.Count, string.Join(", ", SampleRuntimes));

            foreach (var runtime in SampleRuntimes)
            {
                var runtimeOutput = SamplesOutput / runtime;
                runtimeOutput.CreateDirectory();

                foreach (var project in SampleProjects)
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

            Log.Information("Successfully published all samples to {Directory}", SamplesOutput);
        });

    /// <summary>
    /// Packages sample applications into archives for distribution
    /// </summary>
    private Target PackageSamples => td => td
        .DependsOn(PublishSamples)
        .Produces(SamplesOutput / "*.zip")
        .Executes(() =>
        {
            foreach (var runtime in SampleRuntimes)
            {
                var runtimeOutput = SamplesOutput / runtime;
                var archiveName = $"Guinevere-Samples-{VersionFull}-{runtime}.zip";
                var archivePath = SamplesOutput / archiveName;

                Log.Information("Creating samples archive: {Archive}", archiveName);

                runtimeOutput.ZipTo(archivePath, compressionLevel: System.IO.Compression.CompressionLevel.Optimal);
            }

            Log.Information("Successfully packaged all samples");
        });

    /// <summary>
    /// Cleans sample build outputs
    /// </summary>
    private Target CleanSamples => td => td
        .Executes(() =>
        {
            SampleProjects.ForEach(project =>
            {
                (project.Directory / "bin").DeleteDirectory();
                (project.Directory / "obj").DeleteDirectory();
            });

            SamplesOutput.DeleteDirectory();
            Log.Information("Cleaned sample build outputs");
        });

    /// <summary>
    /// Creates a README file for the samples package
    /// </summary>
    private Target CreateSamplesReadme => td => td
        .Before(PackageSamples)
        .Executes(() =>
        {
            foreach (var runtime in SampleRuntimes)
            {
                var runtimeOutput = SamplesOutput / runtime;
                var readmePath = runtimeOutput / "README.md";

                var readmeContent = $@"# Guinevere Samples v{VersionFull}

This package contains sample applications demonstrating the Guinevere GUI system.

## Runtime: {runtime}

## Available Samples

{GetSampleDescriptions()}

## Running the Samples

Each sample is provided as a self-contained executable:

### Windows ({(runtime.Contains("win") ? "Current Platform" : "Not Current Platform")})
- Double-click the `.exe` files to run the samples
- Or run from command line: `SampleName.exe`

### Linux ({(runtime.Contains("linux") ? "Current Platform" : "Not Current Platform")})
- Make executable: `chmod +x SampleName`
- Run from command line: `./SampleName`

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

            Log.Information("Created README files for sample packages");
        });

    /// <summary>
    /// Generates descriptions for all sample projects
    /// </summary>
    private string GetSampleDescriptions()
    {
        var descriptions = SampleProjects
            .OrderBy(p => p.Name)
            .Select(project => $"- **{project.Name}**: {GetSampleDescription(project.Name)}")
            .ToArray();

        return string.Join(Environment.NewLine, descriptions);
    }

    /// <summary>
    /// Gets a description for a sample project based on its name
    /// </summary>
    private static string GetSampleDescription(string projectName) =>
        projectName switch
        {
            "Sample-01" => "Basic Guinevere usage example",
            "Sample-01-OpenGL-OpenTK" => "OpenGL rendering with OpenTK integration",
            "Sample-01-OpenGL-Raylib" => "OpenGL rendering with Raylib integration",
            "Sample-01-OpenGL-SilkNet" => "OpenGL rendering with Silk.NET integration",
            "Sample-01-Vulkan-SilkNet" => "Vulkan rendering with Silk.NET integration",
            "Sample-02-SimpleLayout" => "Demonstrates simple layout system",
            "Sample-03-Layout" => "Shows nested layout with children",
            "Sample-04-Texts" => "Text rendering and typography examples",
            "Sample-05-SingleNodeExpandMargin" => "Layout margin and expansion demo",
            "Sample-41-AdvancedLayoutDemo" => "Advanced layout system features",
            "Sample-42-ResponsiveLayout" => "Responsive design examples",
            "Sample-43-AnimatedLayoutDemo" => "Layout animations and transitions",
            "Sample-50-Controls" => "Basic UI controls demonstration",
            "Sample-75-PaperUI-Dashboard" => "Material Design style dashboard",
            "Sample-70-PanGui-HelloWorld" => "Pan GUI integration - Hello World",
            "Sample-71-PanGui-HelloTriangle" => "Pan GUI integration - Triangle rendering",
            "Sample-72-PanGui-AirbnbSlider" => "Pan GUI integration - Airbnb style slider",
            "Sample-73-PanGui-MusicApp" => "Pan GUI integration - Music player UI",
            "Sample-74-PanGui-Heart" => "Pan GUI integration - Heart animation",
            _ => "Sample application demonstrating Guinevere features"
        };
}
