namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the versioning using GitVersion.
/// </summary>
sealed partial class Build
{
    // NoFetch: GitVersion's own libgit2-based fetch doesn't support SSH remotes, which silently
    // fails injection (gitVersion stays null) when the repo's remotes are SSH URLs. The checkout
    // (local or CI) already has the history/tags it needs, so no additional fetch is required.
    [GitVersion(NoFetch = true)]
    readonly GitVersion GitVersion;

    /// <summary>
    /// The release version. GitVersion decides it; the Conventional Commits fallback only takes over when
    /// GitVersion does not land above the latest release tag (shallow clone, stray older tag). A publish build
    /// checks out a tag on a detached HEAD, where GitVersion cannot run without remote credentials; the tag
    /// being built is the version there.
    /// </summary>
    string Version => CachedVersion ??= ResolveVersion();

    string CachedVersion;

    string ResolveVersion()
    {
        if (GitVersion?.MajorMinorPatch is not { } gitVersion)
        {
            return CurrentVersion;
        }

        if (!HasAnyTags || IsVersionEarlierThan(CurrentVersion, gitVersion))
        {
            return gitVersion;
        }

        var messageLines = Git($"log --format=%B {CurrentTag}..HEAD", logOutput: false).Select(output => output.Text);
        var fallback = BumpVersion(CurrentVersion, messageLines);
        if (fallback != CurrentVersion)
        {
            Log.Warning("GitVersion returned {GitVersion}, which is not above release {Release}. Using {Fallback}",
                gitVersion, CurrentVersion, fallback);
        }

        return fallback;
    }

    public string VersionMajor => Version.Split('.')[0];

    public string VersionMajorMinor => string.Join('.', Version.Split('.').Take(2));

    /// <summary>
    /// The version in a format that can be used as a tag.
    /// </summary>
    string TagName => $"v{Version}";

    /// <summary>
    /// Checks if there are new commits since the last tag that warrant a release.
    /// </summary>
    bool HasNewCommits =>
        GitVersion is not null && GitVersion.CommitsSinceVersionSource != "0" && Version != CurrentVersion;

    string CachedTag;

    /// <summary>
    /// The highest release tag reachable from HEAD, or "0.0.0" before the first release. `describe` is not used
    /// because it selects the closest tag, which is wrong when a stray tag was created after a newer release.
    /// </summary>
    string CurrentTag
    {
        get
        {
            if (CachedTag is null)
            {
                try
                {
                    CachedTag = Git("tag --merged HEAD --sort=-version:refname", logOutput: false)
                        .Select(output => output.Text)
                        .FirstOrDefault(tag => ReleaseTagRegex().IsMatch(tag)) ?? "0.0.0";
                }
                catch
                {
                    CachedTag = "0.0.0";
                }
            }
            return CachedTag;
        }
    }
    string CurrentVersion => CurrentTag.TrimStart('v');

    /// <summary>
    /// Whether a tag is reachable from HEAD (false on the very first release). A tag on a commit outside
    /// HEAD's history does not count: it cannot bound a changelog range.
    /// </summary>
    bool HasAnyTags => CurrentTag != "0.0.0";

    static bool IsVersionEarlierThan(string candidate, string baseline) =>
        System.Version.TryParse(candidate, out var candidateVersion) &&
        System.Version.TryParse(baseline, out var baselineVersion) &&
        candidateVersion < baselineVersion;

    /// <summary>
    /// Returns <paramref name="version"/> bumped by the largest change in the commit message lines, using the
    /// GitVersion.yml rules: major for `type!:` or `BREAKING CHANGE:`, minor for `feat:`, patch for `fix:` and
    /// `perf:`; any other type leaves the version unchanged.
    /// </summary>
    internal static string BumpVersion(string version, IEnumerable<string> messageLines)
    {
        var current = System.Version.Parse(version);
        var bump = messageLines.Select(GetReleaseBump).DefaultIfEmpty(ReleaseBump.None).Max();
        return bump switch
        {
            ReleaseBump.Major => $"{current.Major + 1}.0.0",
            ReleaseBump.Minor => $"{current.Major}.{current.Minor + 1}.0",
            ReleaseBump.Patch => $"{current.Major}.{current.Minor}.{current.Build + 1}",
            _ => version,
        };
    }

    static ReleaseBump GetReleaseBump(string line) =>
        BreakingChangeRegex().IsMatch(line) ? ReleaseBump.Major
        : FeatureRegex().IsMatch(line) ? ReleaseBump.Minor
        : PatchRegex().IsMatch(line) ? ReleaseBump.Patch
        : ReleaseBump.None;

    enum ReleaseBump
    {
        None,
        Patch,
        Minor,
        Major,
    }

    [GeneratedRegex(@"^v\d+\.\d+\.\d+$")]
    private static partial Regex ReleaseTagRegex();

    [GeneratedRegex(@"^\w+(\([\w\-\.]+\))?!:|BREAKING[ -]CHANGE:")]
    private static partial Regex BreakingChangeRegex();

    [GeneratedRegex(@"^feat(\([\w\-\.]+\))?:")]
    private static partial Regex FeatureRegex();

    [GeneratedRegex(@"^fix(\([\w\-\.]+\))?:|^perf(\([\w\-\.]+\))?:")]
    private static partial Regex PatchRegex();

    /// <summary>
    /// Prints the current version.
    /// </summary>
    Target ShowCurrentVersion => td =>
        td
            .Executes(() =>
            {
                Log.Information("Current version:\t\t{Version}", CurrentVersion);
                Log.Information("Current tag:\t\t{Version}", CurrentTag);
                Log.Information("Next version:\t\t{Version}", Version);
            });

    /// <summary>
    /// Stamps the release version into every configured project with a <c>&lt;Version&gt;</c> element, so committed
    /// project versions match the tag and local builds report it.
    /// </summary>
    public Target UpdateProjectVersions => td =>
        td
            .DependsOn(CheckNewCommits)
            .OnlyWhenDynamic(() => HasNewCommits)
            .Executes(() =>
            {
                var projects = VersionedProjects
                    .Where(project => project.Path.ReadAllText().Contains("<Version>", StringComparison.Ordinal));
                foreach (var project in projects)
                {
                    var path = project.Path;
                    var text = File.ReadAllText(path);
                    var stamped = ProjectVersionRegex().Replace(text, $"<Version>{Version}</Version>", 1);
                    if (stamped == text) continue;

                    var bom = File.ReadAllBytes(path) is [0xEF, 0xBB, 0xBF, ..];
                    File.WriteAllText(path, stamped, new UTF8Encoding(bom));
                    Log.Information("{Project}: version {Version}", project.Name, Version);
                }
            });

    [GeneratedRegex(@"<Version>[^<]*</Version>")]
    private static partial Regex ProjectVersionRegex();

    /// <summary>
    /// Checks if there are new commits since the last tag.
    /// If there are no new commits, the whole publish process is skipped.
    /// </summary>
    public Target CheckNewCommits => td =>
        td
            .DependsOn(ShowCurrentVersion)
            .Executes(() =>
            {
                Log.Information("Next version:\t\t{Version}", TagName);
                Log.Information("Checking for new commits...");

                // If there are no new commits since the last tag, skip tag creation
                // Nuke will stop here and not execute any of the following targets
                if (HasNewCommits)
                {
                    Log.Information(
                        "There are {GitVersionCommitsSinceVersionSource} new commits since last tag",
                        GitVersion.CommitsSinceVersionSource
                    );
                }
                else
                {
                    Log.Information("No new commits since last tag. Skipping tag creation");
                }
            });
}
