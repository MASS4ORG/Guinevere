namespace Build;

/// <summary>
/// Defines project-specific settings and feature hooks for the shared MASS4 build.
/// </summary>
sealed partial class Build
{
    const string ProductName = "Guinevere";
    const string DefaultGithubRepository = "MASS4ORG/Guinevere";
    const string DefaultGitlabProjectPath = "MASS4ORG/guinevere";
    const string ReleaseBotEmail = "massa+github@brunomassa.com";
    const string NuGetPackagePrefix = "MASS4.";
    const string PackageAuthors = "Bruno Massa";
    const string PackageCopyrightOwner = "Bruno Massa";
    const string PackageProjectUrl = "https://mass4.org/guinevere";
    const string PackageRepositoryUrl = "https://github.com/MASS4ORG/Guinevere.git";
    const string PackageTags = "gui imgui graphics opengl vulkan skia gamedev opentk raylib silk.net";

    const bool DefaultPublishSelfContained = false;
    const bool DefaultPublishSingleFile = false;
    const bool DefaultPublishTrimmed = false;

    string DefaultConfiguration => IsLocalBuild ? ConfigurationOptions.Debug : ConfigurationOptions.Release;
    const string TestReportArgument = "--report-xunit-trx";

    IEnumerable<Project> ReleaseProjects => PackableProjects;
    IEnumerable<Project> VersionedProjects => PackableProjects;

    AbsolutePath PublishOutput(Project project) => PublishDir / project.Name;
    Dictionary<string, object> MsBuildProperties => [];
    Target[] RestoreDependencies => [];
    Target[] PackDependencies => [Publish];
    Target[] ReleaseCommitDependencies => [UpdateProjectVersions];
}
