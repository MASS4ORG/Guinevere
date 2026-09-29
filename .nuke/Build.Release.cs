using System;
using Nuke.Common;
using Nuke.Common.Tools.Git;
using Serilog;

namespace Build;

/// <summary>Creates the changelog and version commit for a release.</summary>
partial class Build
{
    public Target CreateReleaseCommit => td => td
        .DependsOn(CheckNewCommits, UpdateProjectVersions, UpdateChangelog)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => !string.IsNullOrWhiteSpace(GitHubToken))
        .Executes(() =>
        {
            // Configure git user for CI/CD environment
            GitTasks.Git("config --global user.name `GitHub Actions` ");
            GitTasks.Git("config --global user.email `actions@github.com` ");

            // Use Git commands to commit changes locally
            GitTasks.Git("add .");
            GitTasks.Git($"""commit -m "chore: Automatic commit creation in {Date} [skip ci]" """);
            Log.Information("Created release commit on {Branch}", Repository?.Branch ?? "main");
        });

}
