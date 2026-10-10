namespace Build;

/// <summary>
/// The only build file that differs between the MASS4 repositories besides the feature partials: it names this
/// project and wires its features into the shared build. Names of projects, repositories and products live here.
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

    Project TestProject => Solution.Guinevere_Tests;
    IEnumerable<Project> ReleaseProjects => PackableProjects;
    IEnumerable<Project> VersionedProjects => PackableProjects;

    AbsolutePath PublishOutput(Project project) => PublishDir / project.Name;
    Dictionary<string, object> MsBuildProperties => [];
    Target[] PackDependencies => [Publish];
    Target[] ReleaseCommitDependencies => [UpdateProjectVersions];
}
