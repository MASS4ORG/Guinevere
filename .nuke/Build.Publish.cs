namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the publish process.
/// </summary>
sealed partial class Build
{
    [Parameter(
        "Runtime identifier for the build (e.g., win-x64, linux-x64, osx-x64) (default: linux-x64)"
    )]
    readonly string RuntimeIdentifier = "linux-x64";

    [Parameter("publish-directory (default: ./.publish/{runtimeIdentifier})")]
    readonly AbsolutePath PublishDirectory;

    AbsolutePath PublishDir => PublishDirectory ?? RootDirectory / ".publish" / RuntimeIdentifier;

    [Parameter("publish-self-contained (default: set by the project)")]
    readonly bool PublishSelfContained = DefaultPublishSelfContained;

    [Parameter("publish-single-file (default: set by the project)")]
    readonly bool PublishSingleFile = DefaultPublishSingleFile;

    [Parameter("publish-trimmed (default: set by the project)")]
    readonly bool PublishTrimmed = DefaultPublishTrimmed;

    [Parameter("publish-ready-to-run (default: false)")]
    readonly bool PublishReadyToRun;

    /// <summary>Publishes every release project into its <see cref="PublishOutput"/>.</summary>
    public Target Publish => td =>
        td
            .DependsOn(Restore)
            .Executes(() =>
            {
                var projects = ReleaseProjects.ToList();
                projects.Select(PublishOutput).Distinct().ForEach(directory => directory.CreateOrCleanDirectory());

                foreach (var project in projects)
                {
                    _ = DotNetPublish(settings =>
                    {
                        var configured = WithMsBuildProperties(settings
                            .SetNoLogo(true)
                            .SetProject(project)
                            .SetConfiguration(Config)
                            .SetOutput(PublishOutput(project))
                            .SetRuntime(RuntimeIdentifier)
                            .SetSelfContained(PublishSelfContained)
                            .SetPublishSingleFile(PublishSingleFile)
                            .SetPublishReadyToRun(PublishReadyToRun)
                            .SetPublishTrimmed(PublishTrimmed)
                            .SetVersion(Version)
                            .SetAssemblyVersion(Version)
                            .SetInformationalVersion(Version));
                        ConfigurePublish(project, ref configured);
                        return configured;
                    });
                }

                OnPublished();
            });

    /// <summary>Adjusts the publish settings of one project.</summary>
    partial void ConfigurePublish(Project project, ref DotNetPublishSettings settings);

    /// <summary>Runs after every release project is published, for project specific follow-up steps.</summary>
    partial void OnPublished();
}
