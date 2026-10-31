using System.Diagnostics;

namespace Guinevere.Tests;

public sealed class ReleaseWorkflowTests
{
    [Theory]
    [InlineData("")]
    [InlineData("refs/heads/main")]
    [InlineData("refs/heads/v1.2.3")]
    [InlineData("refs/tags/vbad")]
    [InlineData("refs/tags/v1.2")]
    [InlineData("refs/tags/v1.2.3-rc.1")]
    [InlineData("refs/tags/v01.2.3")]
    [InlineData("refs/tags/v1.2.3+build")]
    public async Task PublishGuardRejectsNonReleaseRefsBeforeAccessingGit(string reference)
    {
        var result = await Run("bash", Path.GetTempPath(), reference, GuardScript);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("stable release tag", result.Output);
    }

    [Theory]
    [InlineData("v0.0.0")]
    [InlineData("v1.20.300")]
    public async Task PublishGuardAcceptsOnlyTheTaggedCheckout(string tag)
    {
        var directory = Path.Combine(Path.GetTempPath(), "guinevere-release-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Equal(0, (await Run("git", directory, "", "init", "--quiet")).ExitCode);
            Assert.Equal(0, (await Commit(directory)).ExitCode);
            Assert.Equal(0, (await Run("git", directory, "", "tag", tag)).ExitCode);
            var accepted = await Run("bash", directory, "refs/tags/" + tag, GuardScript);
            Assert.Equal(0, accepted.ExitCode);
            Assert.Contains("Validated release tag", accepted.Output);
            Assert.Equal(0, (await Commit(directory)).ExitCode);
            var mismatch = await Run("bash", directory, "refs/tags/" + tag, GuardScript);
            Assert.NotEqual(0, mismatch.ExitCode);
            Assert.Contains("does not match", mismatch.Output);
            var missing = await Run("bash", directory, "refs/tags/v9.9.9", GuardScript);
            Assert.NotEqual(0, missing.ExitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ReleaseWorkflowValidatesAndTestsBeforeObtainingPublicationCredentials()
    {
        var publish = File.ReadAllText(Path.Combine(Root, ".github/workflows/publish-on-tag.yml"));
        var validation = publish.IndexOf("run: bash .github/scripts/validate-release-tag.sh", StringComparison.Ordinal);
        var tests = publish.IndexOf("./build.sh TestReport --configuration Release", StringComparison.Ordinal);
        var login = publish.IndexOf("uses: NuGet/login@", StringComparison.Ordinal);
        var push = publish.IndexOf("./build.sh PushNuGet GitHubCreateRelease", StringComparison.Ordinal);
        Assert.True(validation >= 0 && validation < tests && tests < login && login < push);
        Assert.Contains("startsWith(github.ref, 'refs/tags/v')", publish);
        Assert.Contains("--configuration Release", publish[push..]);
        var ci = File.ReadAllText(Path.Combine(Root, ".github/workflows/build-and-test.yml"));
        var release = File.ReadAllText(Path.Combine(Root, ".github/workflows/check-new-release.yml"));
        Assert.Contains("./build.sh TestReport --configuration Release", ci);
        Assert.Contains("./build.sh TestReport GitHubCreateTag", release);
        Assert.Contains("--configuration Release", release);
        Assert.DoesNotContain("--configuration \"Debug\"", ci);
        Assert.True(File.Exists(Path.Combine(Root, "Directory.Build.props")));
        var ignored = IsConfigurationIgnored();
        Assert.False(ignored);
    }

    static bool IsConfigurationIgnored()
    {
        var ignore = File.ReadAllText(Path.Combine(Root, ".gitignore"));
        return ignore.LastIndexOf("!Directory.Build.props", StringComparison.Ordinal)
            < ignore.LastIndexOf("*.Build.props", StringComparison.Ordinal);
    }

    static Task<(int ExitCode, string Output)> Commit(string directory) => Run("git", directory, "",
        "-c", "user.name=Release Test", "-c", "user.email=release-test@example.invalid",
        "-c", "commit.gpgSign=false", "commit", "--quiet", "--allow-empty", "-m", "test");

    static string GuardScript => Path.Combine(Root, ".github/scripts/validate-release-tag.sh");

    static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Guinevere.slnx")))
            {
                directory = directory.Parent;
            }
            return directory!.FullName;
        }
    }

    static async Task<(int ExitCode, string Output)> Run(string executable, string directory, string reference,
        params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["GITHUB_REF"] = reference;
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }
}
