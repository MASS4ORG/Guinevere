using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Serilog;

namespace Build;

/// <summary>
/// This is the main build file for the Guinevere project.
/// GPU accelerated IM GUI system with multi-platform support.
/// </summary>
[ShutdownDotNetAfterServerBuild]
[GitHubActions(
    "ci",
    GitHubActionsImage.UbuntuLatest,
    On = [GitHubActionsTrigger.Push, GitHubActionsTrigger.PullRequest],
    InvokedTargets = [nameof(TestReport), nameof(Compile), nameof(Restore), nameof(Publish)],
    FetchDepth = 0,
    AutoGenerate = false)]
[GitHubActions(
    "daily-release",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    AutoGenerate = false,
    OnCronSchedule = "0 18 * * *", // 15h BRT (18h UTC)
    InvokedTargets = [nameof(Test), nameof(GitHubCreateRelease)])]
// [GitHubActions(
//     "release",
//     GitHubActionsImage.UbuntuLatest,
//     GitHubActionsImage.WindowsLatest,
//     On = new[] { GitHubActionsTrigger.Schedule },
//     OnSchedule = "0 0 * * 4", // Every Thursday at midnight UTC
//     InvokedTargets = new[] { nameof(PublishAll) })]
internal sealed partial class Build : NukeBuild
{
    private static int Main() => Execute<Build>(x => x.Compile);

    /// <summary>
    /// Complete CI pipeline: Clean, Restore, Compile, and Test
    /// </summary>
    private Target Ci => td => td
        .DependsOn(Clean, Restore, Compile, Test)
        .Executes(() => Log.Information("CI pipeline completed successfully"));

    /// <summary>
    /// Complete release pipeline: Build, Test, Package, and Publish
    /// </summary>
    private Target Release => td => td
        .DependsOn(
        // CI,
        PublishNuGet
        // , PublishExamples, PackageExamples
        )
        .Executes(() => Log.Information("Release pipeline completed successfully"));

    /// <summary>
    /// Build all deliverables without publishing
    /// </summary>
    private Target BuildAll => td => td
        .DependsOn(Compile, BuildExamples, PackNuGet, PackageExamples)
        .Executes(() => Log.Information("All deliverables built successfully"));
}
