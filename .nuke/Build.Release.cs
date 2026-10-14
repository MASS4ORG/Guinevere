namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for tagging releases and publishing them to GitLab/GitHub,
/// including uploading the packed artifacts produced by <see cref="Pack"/>.
/// </summary>
sealed partial class Build
{
    [Parameter($"GitLab group/project path (default: {DefaultGitlabProjectPath})")]
    readonly string GitlabProjectPath = DefaultGitlabProjectPath;

    [Parameter($"GitHub owner/repo path (default: {DefaultGithubRepository})")]
    readonly string GithubRepository = DefaultGithubRepository;

    [Parameter("Branch the release commit is pushed to (default: main)")]
    readonly string ReleaseBranch = "main";

    [Parameter("GitLab personal/project access token with api + write_repository scope. Omit to skip pushing/releasing to GitLab.")]
    [Secret]
    readonly string GitlabToken;

    [Parameter("GitHub token (repo scope, or the Actions-provided GITHUB_TOKEN) with contents:write. Omit to skip pushing/releasing to GitHub.")]
    [Secret]
    readonly string GithubToken;

    /// <summary>
    /// Commits the changelog and whatever <see cref="ReleaseCommitDependencies"/> change once before creating the
    /// release tag.
    /// </summary>
    public Target CreateReleaseCommit => td => td
        .DependsOn(UpdateChangelog)
        .DependsOn(ReleaseCommitDependencies)
        .OnlyWhenDynamic(() => HasNewCommits)
        .Executes(() =>
        {
            RunGit("set release bot name", "config", "user.name", $"{ProductName} Bot");
            RunGit("set release bot email", "config", "user.email", ReleaseBotEmail);
            RunGit("commit changelog", "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-am", $"chore(release): {Version}");
        });

    public Target CreateReleaseTag => td => td
        .DependsOn(CreateReleaseCommit)
        .OnlyWhenDynamic(() => HasNewCommits)
        .Executes(() => RunGit("tag release", "-c", "tag.gpgsign=false", "tag", TagName));

    void PushToRemote(string name, string token, string repositoryPath, string host, string credentialUser)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Log.Information("No token supplied for {Name}; skipping push", name);
            return;
        }

        var url = $"https://{credentialUser}:{token}@{host}/{repositoryPath}.git";

        var existingTag = RunGit("check remote tag", "ls-remote", "--tags", url, TagName);
        if (!string.IsNullOrWhiteSpace(existingTag))
        {
            Log.Information("{Name} already has tag {TagName}; skipping push", name, TagName);
            return;
        }

        RunGit("push release commit", "push", url, $"HEAD:{ReleaseBranch}");
        RunGit("push release tag", "push", url, TagName);
        Log.Information("Pushed {TagName} to {Name}", TagName, name);
    }

    string RunGit(string operation, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = RootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start git for {operation}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var standardOutput = output.GetAwaiter().GetResult();
        var standardError = error.GetAwaiter().GetResult();

        if (process.ExitCode == 0)
        {
            return standardOutput;
        }

        var message = standardError;
        if (!string.IsNullOrEmpty(GithubToken))
        {
            message = message.Replace(GithubToken, "[redacted]", StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(GitlabToken))
        {
            message = message.Replace(GitlabToken, "[redacted]", StringComparison.Ordinal);
        }

        throw new InvalidOperationException($"Git {operation} failed (exit {process.ExitCode}): {message.Trim()}");
    }
}
