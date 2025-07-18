using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.Git;

using Serilog;

namespace Guinevere.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for creating GitHub releases and uploading assets.
/// </summary>
partial class Build
{
    [Parameter("GitHub token for creating releases")]
    public readonly string GitHubToken;

    [Parameter("GitHub repository")]
    public readonly string GitHubRepository = "brmassa/guinevere";

    [Parameter("GitHub API URL")]
    private static readonly string GitHubApiBaseUrl = "https://api.github.com";

    [Parameter("Skip GitHub release creation")]
    public readonly bool SkipGitHubRelease;

    private static string Date =>
        DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Creates a GitHub release with all assets following SumTree pattern
    /// </summary>
    public Target GitHubCreateRelease => td => td
        .DependsOn(GitHubCreateTag, ExtractChangelogUnreleased)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            try
            {
                using var httpClient = HttpClientGitHubToken();
                var message = $"{ChangelogUnreleased}";
                var release = $"{TagName} / {Date}";
                var response = await httpClient.PostAsJsonAsync(
                    GitHubApiUrl($"repos/{GitHubRepository}/releases"),
                    new
                    {
                        tag_name = TagName,
                        name = release,
                        body = message,
                        draft = false,
                        prerelease = IsPreRelease()
                    }).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
                Log.Information(
                    "Release {release} created with the description '{message}'",
                    release, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Creates a GitHub release with all assets (legacy method)
    /// </summary>
    private Target CreateGitHubRelease => td => td
        .DependsOn(PackNuGet, PackageSamples, UpdateChangelog)
        .OnlyWhenStatic(() => !string.IsNullOrEmpty(GitHubToken))
        .OnlyWhenStatic(() => HasNewCommits)
        .OnlyWhenStatic(() => !SkipGitHubRelease)
        .Executes(async () =>
        {
            var tagName = TagName;
            var releaseName = $"Guinevere v{VersionFull}";
            var releaseBody = GetReleaseNotes();

            Log.Information("Creating GitHub release: {ReleaseName}", releaseName);

            // Create the release
            var releaseId = await CreateGitHubReleaseAsync(tagName, releaseName, releaseBody);

            if (releaseId.HasValue)
            {
                // Upload NuGet packages
                await UploadNuGetPackagesAsync(releaseId.Value);

                // Upload sample packages
                await UploadSamplePackagesAsync(releaseId.Value);

                Log.Information("Successfully created GitHub release with all assets");
            }
        });

    /// <summary>
    /// Creates a tag in the GitHub repository following SumTree pattern
    /// </summary>
    private Target GitHubCreateTag => td => td
        .DependsOn(CheckNewCommits, GitHubCreateCommit)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            try
            {
                using var httpClient = HttpClientGitHubToken();
                var message = $"Automatic tag creation: '{TagName}' in {Date}";
                var response = await httpClient.PostAsJsonAsync(
                    GitHubApiUrl($"repos/{GitHubRepository}/git/refs"),
                    new
                    {
                        @ref = $"refs/tags/{TagName}",
                        sha = GitTasks.Git("rev-parse HEAD").FirstOrDefault().Text
                    }).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
                Log.Information(
                    "Tag {tag} created with the message '{message}'",
                    TagName, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Creates a tag and pushes it to GitHub (legacy method)
    /// </summary>
    private Target CreateTag => td => td
        .DependsOn(CreateCommit)
        .OnlyWhenStatic(() => HasNewCommits)
        .Executes(() =>
        {
            try
            {
                Log.Information("Creating and pushing tag: {TagName}", TagName);

                // Create the tag
                GitTasks.Git($"tag -a {TagName} -m \"Release {VersionFull}\"");

                // Push the tag
                GitTasks.Git($"push origin {TagName}");

                Log.Information("Successfully created and pushed tag: {TagName}", TagName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or pushing tag");
                throw;
            }
        });

    private Target GitHubCreateCommit => td => td
        .DependsOn(CheckNewCommits, UpdateProjectVersions, UpdateChangelog)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => !string.IsNullOrWhiteSpace(GitHubToken))
        .Executes(() =>
        {
            // Configure git user for CI/CD environment
            GitTasks.Git("config --global user.name \"github-actions\"");
            GitTasks.Git("config --global user.email \"github-actions@github.com\"");

            // Use Git commands to commit changes locally
            GitTasks.Git("add -A");
            GitTasks.Git($"commit -m \"chore: Automatic commit creation in {Date} [skip ci]\"");
            GitTasks.Git("push origin HEAD");

            Log.Information(
                "Commit in branch {branch} created and pushed",
                Repository?.Branch ?? "main");
        });

    /// <summary>
    /// Complete release process: commit, tag, and create GitHub release
    /// </summary>
    private Target PublishAll => td => td
        .DependsOn(Test, Compile, PackNuGet, PackageSamples, CreateTag, CreateGitHubRelease, PublishNuGet)
        .Executes(() =>
        {
            Log.Information("Completed full release process for version {Version}", VersionFull);
        });

    /// <summary>
    /// Creates a GitHub release using the GitHub API
    /// </summary>
    private async Task<long?> CreateGitHubReleaseAsync(string tagName, string name, string body)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"token {GitHubToken}");
        client.DefaultRequestHeaders.Add("User-Agent", "Guinevere-Build-System");

        var releaseData = new
        {
            tag_name = tagName,
            target_commitish = "main",
            name = name,
            body = body,
            draft = false,
            prerelease = IsPreRelease()
        };

        var json = JsonSerializer.Serialize(releaseData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(
                $"https://api.github.com/repos/{GitHubRepository}/releases",
                content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var id = doc.RootElement.GetProperty("id").GetInt64();

                Log.Information("Created GitHub release with ID: {ReleaseId}", id);
                return id;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to create GitHub release. Status: {StatusCode}, Content: {Content}",
                    response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred while creating GitHub release");
            return null;
        }
    }

    /// <summary>
    /// Uploads NuGet packages as release assets
    /// </summary>
    private async Task UploadNuGetPackagesAsync(long releaseId)
    {
        var packages = PackagesDirectory.GlobFiles("*.nupkg").ToList();

        Log.Information("Uploading {Count} NuGet packages to GitHub release", packages.Count);

        foreach (var package in packages)
        {
            await UploadReleaseAssetAsync(releaseId, package, "application/zip");
        }
    }

    /// <summary>
    /// Uploads sample packages as release assets
    /// </summary>
    private async Task UploadSamplePackagesAsync(long releaseId)
    {
        var samplePackages = SamplesOutput.GlobFiles("*.zip").ToList();

        Log.Information("Uploading {Count} sample packages to GitHub release", samplePackages.Count);

        foreach (var package in samplePackages)
        {
            await UploadReleaseAssetAsync(releaseId, package, "application/zip");
        }
    }

    /// <summary>
    /// Uploads a file as a GitHub release asset
    /// </summary>
    private async Task UploadReleaseAssetAsync(long releaseId, AbsolutePath filePath, string contentType)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"token {GitHubToken}");
        client.DefaultRequestHeaders.Add("User-Agent", "Guinevere-Build-System");

        var fileName = filePath.Name;
        var fileBytes = File.ReadAllBytes(filePath);

        using var content = new ByteArrayContent(fileBytes);
        content.Headers.Add("Content-Type", contentType);

        try
        {
            var uploadUrl = $"https://uploads.github.com/repos/{GitHubRepository}/releases/{releaseId}/assets?name={fileName}";
            var response = await client.PostAsync(uploadUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Log.Information("Successfully uploaded asset: {FileName}", fileName);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to upload asset {FileName}. Status: {StatusCode}, Content: {Content}",
                    fileName, response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred while uploading asset: {FileName}", fileName);
        }
    }

    /// <summary>
    /// Gets the release notes from the changelog
    /// </summary>
    private string GetReleaseNotes()
    {
        try
        {
            if (!File.Exists(ChangelogFile))
            {
                return GetDefaultReleaseNotes();
            }

            var changelogContent = File.ReadAllText(ChangelogFile);
            var versionSection = ExtractVersionSection(changelogContent, VersionFull);

            return string.IsNullOrEmpty(versionSection) ? GetDefaultReleaseNotes() : versionSection;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not extract release notes from changelog, using default");
            return GetDefaultReleaseNotes();
        }
    }

    /// <summary>
    /// Extracts the section for a specific version from the changelog
    /// </summary>
    private static string ExtractVersionSection(string changelogContent, string version)
    {
        var lines = changelogContent.Split('\n');
        var startIndex = -1;
        var endIndex = -1;

        // Find the start of the version section
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains($"## v[{version}]") || lines[i].Contains($"## [{version}]"))
            {
                startIndex = i + 1; // Start after the header
                break;
            }
        }

        if (startIndex == -1) return string.Empty;

        // Find the end of the version section (next version header or end of file)
        for (var i = startIndex; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ") && i > startIndex)
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1) endIndex = lines.Length;

        // Extract and clean the section
        var sectionLines = lines[startIndex..endIndex]
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join('\n', sectionLines).Trim();
    }

    /// <summary>
    /// Gets default release notes when changelog is not available
    /// </summary>
    private string GetDefaultReleaseNotes() => $@"# Guinevere v{VersionFull}

## What's New

This release includes the latest improvements and bug fixes for the Guinevere GPU accelerated IM GUI system.

## Downloads

### NuGet Packages
- **org.mass4.Guinevere**: Core Guinevere library
- **org.mass4.Guinevere.OpenGL.OpenTK**: OpenTK integration
- **org.mass4.Guinevere.OpenGL.Raylib**: Raylib integration
- **org.mass4.Guinevere.OpenGL.SilkNET**: Silk.NET OpenGL integration
- **org.mass4.Guinevere.Vulkan.SilkNET**: Silk.NET Vulkan integration

### Sample Applications
- **Guinevere-Samples-{VersionFull}-win-x64.zip**: Windows samples
- **Guinevere-Samples-{VersionFull}-linux-x64.zip**: Linux samples

## About Guinevere

Guinevere is a GPU accelerated immediate mode GUI system built on SkiaSharp. It provides high-performance rendering with modern graphics APIs support.

- **Website**: https://mass4.org
- **Source Code**: https://github.com/brmassa/guinevere
- **Author**: Bruno Massa (massa@brunomassa.com)

## Support

For questions, issues, or contributions, please visit our [GitHub repository](https://github.com/brmassa/guinevere).";

    /// <summary>
    /// Determines if this is a pre-release version
    /// </summary>
    private bool IsPreRelease() =>
        VersionFull.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
        VersionFull.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
        VersionFull.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
        VersionFull.Contains("preview", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates an HTTP client and set the authentication header.
    /// </summary>
    private HttpClient HttpClientGitHubToken()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Guinevere-Build-System");
        return httpClient;
    }

    /// <summary>
    /// Generate the GitHub API URL.
    /// </summary>
    /// <param name="url">The URL to append to the base URL.</param>
    /// <returns></returns>
    private string GitHubApiUrl(string url)
    {
        var apiUrl = $"{GitHubApiBaseUrl}/{url}";
        Log.Information("GitHub API call: {url}", apiUrl);
        return apiUrl;
    }
}
