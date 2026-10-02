namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the versioning using GitVersion.
/// </summary>
partial class Build
{
    [GitRepository]
    private readonly GitRepository Repository;

    [GitVersion]
    private readonly GitVersion GitVersion;

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

                // GitVersion uses the nearest version source. A stray or incorrectly-created older tag can
                // therefore make it regress below the version calculated from the released tag history.
                if (IsVersionEarlierThan(gitVersionValue, calculatedVersion))
                {
                    Log.Warning(
                        "GitVersion returned {GitVersion}, but released tags require at least {Fallback}. Using fallback",
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

    /// <summary>
    /// The version in a format that can be used as a tag.
    /// </summary>
    private string TagName => $"v{VersionFull}";

    /// <summary>
    /// Checks if there are new commits since the last tag.
    /// </summary>
    private bool HasNewCommits =>
        GitVersion != null ? GitVersion.CommitsSinceVersionSource != "0" : GetCommitsSinceLastTag() > 0;

    private string CurrentVersion;

    private string CurrentTag
    {
        get
        {
            if (CurrentVersion != null)
                return CurrentVersion;

            try
            {
                // `describe` selects the closest tag, which is unsafe if a stale tag was accidentally
                // created after a newer release. Select the highest semantic-version tag reachable from HEAD.
                var gitTag = GitTasks.Git("tag --merged HEAD --sort=-version:refname")
                    .Select(output => output.Text)
                    .FirstOrDefault(IsReleaseTag);
                CurrentVersion = SelectLatestReleaseTag(gitTag, GetLatestChangelogTag());
            }
            catch
            {
                // Handled below so an empty repository still has a deterministic initial version.
            }

            CurrentVersion ??= "v1.0.0";

            return CurrentVersion;
        }
    }

    private string CurrentFullVersion => CurrentTag.TrimStart('v');

    private static bool IsReleaseTag(string tag) =>
        tag.StartsWith('v') && Version.TryParse(tag[1..], out var version) && version.Build >= 0;

    private static bool IsVersionEarlierThan(string candidate, string baseline) =>
        Version.TryParse(candidate, out var candidateVersion) &&
        Version.TryParse(baseline, out var baselineVersion) &&
        candidateVersion < baselineVersion;

    private string GetLatestChangelogTag()
    {
        if (!File.Exists(ChangelogFile))
            return null;

        return VersionRegex().Matches(File.ReadAllText(ChangelogFile))
            .Select(match => $"v{match.Groups[1].Value}")
            .Where(IsReleaseTag)
            .OrderByDescending(tag => Version.Parse(tag[1..]))
            .FirstOrDefault();
    }

    private static string SelectLatestReleaseTag(params string[] tags) =>
        tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
            .OrderByDescending(tag => Version.Parse(tag[1..]))
            .FirstOrDefault();

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
                Log.Information("GitVersion available - commits since last version: {Commits}",
                    GitVersion.CommitsSinceVersionSource);
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
                if (HasNewCommits)
                    Log.Information("There are {GitVersionCommitsSinceVersionSource} new commits since last tag",
                        GitVersion.CommitsSinceVersionSource);
                else
                    Log.Information("No new commits since last tag. Skipping tag creation");
            }
            else
            {
                Log.Warning("GitVersion not available - continuing with CI/CD pipeline");
                Log.Information("Using fallback versioning strategy");
            }
        });

    /// <summary>
    /// Stamps the release version into every project that ships a package, so the committed
    /// csproj version matches the tag. Test, build and example projects are left alone.
    /// </summary>
    private Target UpdateProjectVersions => td => td
        .DependsOn(CheckNewCommits)
        .Executes(() =>
        {
            Log.Information("Projects: {ProjectsCount}", PackageableProjects.Count);

            PackageableProjects.ForEach(project =>
            {
                if (project == null) return;
                Log.Information("{Project}:\tfrom {Version} to {VersionFull}",
                    project.Name, project.GetProperty("Version"), VersionFull);
                var msbuildProject = project.GetMSBuildProject();
                msbuildProject.SetProperty("Version", VersionFull);
                msbuildProject.Save(project.Path);
            });
        });
}
