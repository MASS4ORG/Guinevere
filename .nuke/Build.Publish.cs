using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace Guinevere.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the publish process.
/// </summary>
partial class Build
{
    [Parameter("Runtime identifier for the build (e.g., win-x64, linux-x64, osx-x64) (default: linux-x64)")]
    public readonly string RuntimeIdentifier = "linux-x64";

    [Parameter("publish-directory (default: ./publish/{runtimeIdentifier})")]
    public readonly AbsolutePath PublishDirectory;
    private AbsolutePath PublishDir => PublishDirectory ?? RootDirectory / "publish" / RuntimeIdentifier;

    [Parameter("publish-self-contained (default: true)")]
    public readonly bool PublishSelfContained = true;

    [Parameter("publish-single-file (default: false - only for executables)")]
    public readonly bool PublishSingleFile;

    [Parameter("publish-trimmed (default: false - libraries should not be trimmed)")]
    public readonly bool PublishTrimmed;

    [Parameter("publish-ready-to-run (default: false - not applicable for libraries)")]
    public readonly bool PublishReadyToRun;

    /// <summary>
    /// Simple publish target for compilation testing - equivalent to dotnet publish
    /// </summary>
    private Target Publish => td => td
        .After(Restore)
        .Executes(() =>
        {
            Log.Information("Publishing Guinevere core library for {Runtime}", RuntimeIdentifier);

            DotNetTasks.DotNetPublish(s => s
                // .SetProject(Solution.Guinevere)
                .SetConfiguration(ConfigurationSet)
                .SetOutput(PublishDir)
                .SetRuntime(RuntimeIdentifier)
                .SetSelfContained(PublishSelfContained)
                .SetVersion(VersionFull)
                .SetAssemblyVersion(VersionFull)
                .SetInformationalVersion(VersionFull));

            if (PublishSingleFile)
            {
                Log.Warning("PublishSingleFile is only supported for executable applications, skipping for library project");
            }

            if (PublishTrimmed)
            {
                Log.Warning("PublishTrimmed is not recommended for library projects, skipping");
            }

            if (PublishReadyToRun)
            {
                Log.Warning("PublishReadyToRun is not applicable for library projects, skipping");
            }

            Log.Information("Successfully published Guinevere to {Directory}", PublishDir);
        });

    /// <summary>
    /// Publishes the core Guinevere library for distribution
    /// </summary>
    private Target PublishLibrary => td => td
        .After(Compile)
        .Executes(() =>
        {
            Log.Information("Publishing Guinevere core library for distribution - {Runtime}", RuntimeIdentifier);

            _ = DotNetTasks.DotNetPublish(s => s
                .SetProject(Solution.Guinevere)
                .SetConfiguration(ConfigurationSet)
                .SetOutput(PublishDir / "library")
                .SetRuntime(RuntimeIdentifier)
                .SetSelfContained(PublishSelfContained)
                .SetVersion(VersionFull)
                .SetAssemblyVersion(VersionFull)
                .SetInformationalVersion(VersionFull));

            Log.Information("Successfully published Guinevere library to {Directory}", PublishDir / "library");
        });

    /// <summary>
    /// Publishes all integration libraries for distribution
    /// </summary>
    private Target PublishIntegrations => td => td
        .After(Compile)
        .Executes(() =>
        {
            var integrationProjects = new[]
            {
                Solution.Integrations.Guinevere_OpenGL_OpenTK,
                Solution.Integrations.Guinevere_OpenGL_Raylib,
                Solution.Integrations.Guinevere_OpenGL_SilkNET,
                Solution.Integrations.Guinevere_Vulkan_SilkNET
            };

            foreach (var project in integrationProjects)
            {
                if (project == null) continue;
                var projectOutput = PublishDir / "integrations" / project.Name;

                Log.Information("Publishing integration {Project} for {Runtime}", project.Name, RuntimeIdentifier);

                _ = DotNetTasks.DotNetPublish(s => s
                    .SetProject(project)
                    .SetConfiguration(ConfigurationSet)
                    .SetOutput(projectOutput)
                    .SetRuntime(RuntimeIdentifier)
                    .SetSelfContained(PublishSelfContained)
                    .SetPublishSingleFile(PublishSingleFile)
                    .SetPublishReadyToRun(PublishReadyToRun)
                    .SetPublishTrimmed(PublishTrimmed)
                    .SetVersion(VersionFull)
                    .SetAssemblyVersion(VersionFull)
                    .SetInformationalVersion(VersionFull)
                    .AddProperty("TrimMode", "partial")
                    .AddProperty("EnableTrimAnalyzer", PublishTrimmed)
                );
            }

            Log.Information("Successfully published all integrations");
        });

    /// <summary>
    /// Publishes all binaries (library + integrations)
    /// </summary>
    private Target PublishBinaries => td => td
        .DependsOn(PublishLibrary, PublishIntegrations)
        .Executes(() =>
        {
            Log.Information("All binary publish operations completed successfully");
        });
}
