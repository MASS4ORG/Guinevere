using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.GitVersion;
using Serilog;

namespace Guinevere.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the versioning using GitVersion.
/// </summary>
partial class Build
{
    [GitRepository] private readonly GitRepository Repository;

    [GitVersion] private readonly GitVersion GitVersion;

    /// <summary>
    /// The current version, using GitVersion with fallback.
    /// </summary>
    private string VersionFull
    {
        get
        {
            if (GitVersion?.MajorMinorPatch != null)
            {
                var gitVersionValue = GitVersion.MajorMinorPatch;
                var calculatedVersion = CalculateNextVersion();

                // Use fallback if GitVersion gives unreasonable result (0.x.x when we have tagged releases > 1.0)
                if (gitVersionValue.StartsWith("0.") && !calculatedVersion.StartsWith("0."))
                {
                    Log.Warning("GitVersion returned {GitVersion} but fallback calculated {Fallback}. Using fallback.",
                        gitVersionValue, calculatedVersion);
                    return calculatedVersion;
                }

                Log.Debug("Using GitVersion: {Version}", gitVersionValue);
                return gitVersionValue;
            }

            var fallbackVersion = CalculateNextVersion();
            Log.Debug("Using fallback version calculation: {Version}", fallbackVersion);
            return fallbackVersion;
        }
    }

    private string VersionMajor => GitVersion?.Major.ToString(CultureInfo.InvariantCulture) ?? GetFallbackMajor();

    private string VersionMajorMinor =>
        GitVersion != null ? $"{GitVersion.Major}.{GitVersion.Minor}" : GetFallbackMajorMinor();

    /// <summary>
    /// The version in a format that can be used as a tag.
    /// </summary>
    private string TagName => $"v{VersionFull}";

    /// <summary>
    /// Checks if there are new commits since the last tag.
    /// </summary>
    private bool HasNewCommits => GitVersion != null ? GitVersion.CommitsSinceVersionSource != "0" : GetCommitsSinceLastTag() > 0;

    private string CurrentVersion;

    private string CurrentTag
    {
        get
        {
            if (CurrentVersion != null)
                return CurrentVersion;

            try
            {
                CurrentVersion = GitTasks.Git("describe --tags --abbrev=0")
                    .FirstOrDefault().Text;
            }
            catch
            {
                CurrentVersion = "v1.0.0";
            }

            return CurrentVersion;
        }
    }

    private string CurrentFullVersion => CurrentTag.TrimStart('v');

    /// <summary>
    /// Calculates the next version by incrementing from the last tag
    /// </summary>
    private string CalculateNextVersion()
    {
        var commitsSinceTag = GetCommitsSinceLastTag();
        Log.Debug("Commits since last tag: {Count}", commitsSinceTag);

        if (commitsSinceTag == 0)
        {
            Log.Debug("No commits since tag, returning current version: {Version}", CurrentFullVersion);
            return CurrentFullVersion;
        }

        var currentVersion = CurrentFullVersion;
        Log.Debug("Current tag version: {Version}", currentVersion);
        var parts = currentVersion.Split('.');

        if (parts.Length >= 2)
        {
            if (int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
            {
                // Increment minor version for new commits
                var nextVersion = $"{major}.{minor + 1}.0";
                Log.Debug("Calculated next version: {Version}", nextVersion);
                return nextVersion;
            }
        }

        // Fallback to current version if parsing fails
        Log.Warning("Could not parse version {Version}, returning as-is", currentVersion);
        return currentVersion;
    }

    /// <summary>
    /// Gets the number of commits since the last tag
    /// </summary>
    private int GetCommitsSinceLastTag()
    {
        try
        {
            // Count commits between tag and HEAD
            var commitCountText = GitTasks.Git($"rev-list --count {CurrentTag}..HEAD")
                .FirstOrDefault().Text;

            if (int.TryParse(commitCountText, out var directCount))
                return directCount;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not calculate commits since last tag");
        }

        return 1; // Assume there are changes if we can't determine
    }

    /// <summary>
    /// Gets fallback major version when GitVersion is not available.
    /// </summary>
    private string GetFallbackMajor()
    {
        var version = CurrentFullVersion;
        var parts = version.Split('.');
        return parts.Length > 0 ? parts[0] : "1";
    }

    /// <summary>
    /// Gets fallback major.minor version when GitVersion is not available.
    /// </summary>
    private string GetFallbackMajorMinor()
    {
        var version = CurrentFullVersion;
        var parts = version.Split('.');
        if (parts.Length >= 2)
            return $"{parts[0]}.{parts[1]}";
        return parts.Length == 1 ? $"{parts[0]}.0" : "1.0";
    }

    /// <summary>
    /// Gets version from environment variables (useful for CI/CD).
    /// </summary>
    private string GetEnvironmentVersion()
    {
        // Check for GitHub Actions tag reference
        var githubRef = Environment.GetEnvironmentVariable("GITHUB_REF");
        if (!string.IsNullOrEmpty(githubRef) && githubRef.StartsWith("refs/tags/"))
        {
            var tag = githubRef.Substring("refs/tags/".Length);
            if (tag.StartsWith("v") && IsValidVersion(tag.Substring(1)))
                return tag;
        }

        return null;
    }

    /// <summary>
    /// Validates if a string is a valid semantic version.
    /// </summary>
    private bool IsValidVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return false;

        var parts = version.Split('.');
        if (parts.Length < 2 || parts.Length > 4) return false;

        return parts.Take(3).All(part => int.TryParse(part, out _));
    }

    /// <summary>
    /// Prints the current version.
    /// </summary>
    private Target ShowCurrentVersion => td => td
        .Executes(() =>
        {
            Log.Information("Current version:  {Version}", CurrentFullVersion);
            Log.Information("Current tag:      {Version}", CurrentTag);
            Log.Information("Next version:     {Version}", VersionFull);

            if (GitVersion == null)
            {
                Log.Warning("GitVersion is not available - using fallback version from git tags");
                var envVersion = GetEnvironmentVersion();
                if (envVersion != null)
                {
                    Log.Information("Environment version detected: {Version}", envVersion);
                }
            }
            else
            {
                Log.Information("GitVersion available - commits since last version: {Commits}", GitVersion.CommitsSinceVersionSource);
            }
        });

    /// <summary>
    /// Checks if there are new commits since the last tag.
    /// If there are no new commits, the whole publish process is skipped.
    /// </summary>
    private Target CheckNewCommits => td => td
        .DependsOn(ShowCurrentVersion)
        .Executes(() =>
        {
            Log.Information("Next version:    {Version}", TagName);

            if (GitVersion != null)
            {
                // If there are no new commits since the last tag, skip tag creation
                // Nuke will stop here and not execute any of the following targets
                Log.Information(HasNewCommits
                    ? $"There are {GitVersion.CommitsSinceVersionSource} new commits since last tag."
                    : "No new commits since last tag. Skipping tag creation.");
            }
            else
            {
                Log.Warning("GitVersion not available - continuing with CI/CD pipeline");
                Log.Information("Using fallback versioning strategy");
            }
        });

    /// <summary>
    /// Update each project Version
    /// </summary>
    private Target UpdateProjectVersions => td => td
        .DependsOn(CheckNewCommits)
        .Executes(() =>
        {
            Log.Information("Projects: {ProjectsCount}",
                Solution.Projects.Count);

            var projectsToVersion = new List<Project>
            {
                Solution.Guinevere,
                Solution.GetProject("Guinevere.Scripting"),
                Solution.GetProject("Guinevere.OpenGL.OpenTK"),
                Solution.GetProject("Guinevere.OpenGL.Raylib"),
                Solution.GetProject("Guinevere.OpenGL.SilkNET"),
                Solution.GetProject("Guinevere.Vulkan.SilkNET")
            };

            projectsToVersion.ForEach(project =>
            {
                if (project == null) return;
                Log.Information(
                    "{project}:\tfrom {version} to {VersionFull}",
                    project.Name,
                    project.GetProperty("Version"), VersionFull);
                var msbuildProject = project.GetMSBuildProject();
                msbuildProject.SetProperty("Version", VersionFull);
                msbuildProject.Save(project.Path);
            });
        });
}
