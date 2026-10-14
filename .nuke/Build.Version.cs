namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial calculates release versions from reachable release tags and Conventional Commit messages.
/// </summary>
sealed partial class Build
{
    /// <summary>
    /// The release version derived from the highest reachable release tag and commits after it.
    /// </summary>
    string Version => CachedVersion ??= ResolveVersion();

    string CachedVersion;

    string ResolveVersion()
    {
        var range = HasAnyTags ? $"{CurrentTag}..HEAD" : "HEAD";
        var messageLines = Git($"log --format=%B {range}", logOutput: false).Select(static output => output.Text);
        return BumpVersion(CurrentVersion, messageLines);
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
    bool HasNewCommits => CommitsSinceCurrentTag > 0 && Version != CurrentVersion;

    int CommitsSinceCurrentTag
    {
        get
        {
            try
            {
                var range = HasAnyTags ? $"{CurrentTag}..HEAD" : "HEAD";
                return int.Parse(Git($"rev-list --count {range}", logOutput: false).Single().Text, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }
    }

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
                        .Select(static output => output.Text)
                        .FirstOrDefault(static tag => ReleaseTagRegex().IsMatch(tag)) ?? "0.0.0";
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

    /// <summary>
    /// Returns <paramref name="version"/> bumped by the largest change in the commit message lines, using the
    /// release rules: major for `type!:` or `BREAKING CHANGE:`, minor for `feat:`, patch for `fix:` and
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
                    .Where(static project => project.Path.ReadAllText().Contains("<Version>", StringComparison.Ordinal));
                foreach (var project in projects)
                {
                    var path = project.Path;
                    var text = File.ReadAllText(path);
                    var stamped = ProjectVersionRegex().Replace(text, $"<Version>{Version}</Version>", 1);
                    if (stamped == text)
                    {
                        continue;
                    }

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
                        "There are {CommitsSinceCurrentTag} new commits since last tag",
                        CommitsSinceCurrentTag
                    );
                }
                else
                {
                    Log.Information("No new commits since last tag. Skipping tag creation");
                }
            });
}
