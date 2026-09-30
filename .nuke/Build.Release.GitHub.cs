namespace Build;

/// <summary>
/// Creates GitHub release tags and releases.
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
    [PublicAPI]
    Target GitHubCreateRelease => td => td
        .DependsOn(GitHubCreateTag, ExtractChangelogUnreleased)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            try
            {
                using var httpClient = HttpClientGitHubToken();
                var body = ChangelogUnreleased;
                var release = $"{TagName} / {Date}";
                var response = await httpClient.PostAsJsonAsync(
                    GitHubApiUrl($"repos/{GitHubRepository}/releases"),
                    new
                    {
                        tag_name = TagName,
                        name = release,
                        body,
                        draft = false,
                        prerelease = IsPreRelease()
                    }).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
                Log.Information("Release {Release} created with the description '{Message}'", release, body);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Creates a tag in the GitHub repository following SumTree pattern
    /// </summary>
    [PublicAPI]
    Target GitHubCreateTag => td => td
        .DependsOn(CheckNewCommits, CreateReleaseCommit)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            try
            {
                GitTasks.Git("push origin HEAD");
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
                Log.Information("Tag {Tag} created with the message '{Message}'", TagName, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Creates a GitHub release using the GitHub API
    /// </summary>
    [PublicAPI]
    async Task<long?> CreateGitHubReleaseAsync(string tagName, string name, string body)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"token {GitHubToken}");
        client.DefaultRequestHeaders.Add("User-Agent", "Guinevere-Build-System");

        var releaseData = new
        {
            tag_name = tagName,
            target_commitish = "main",
            name,
            body,
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
    [PublicAPI]
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
    /// Uploads example packages as release assets
    /// </summary>
    [PublicAPI]
    async Task UploadExamplePackagesAsync(long releaseId)
    {
        var examplePackages = ExamplesOutput.GlobFiles("*.zip").ToList();

        Log.Information("Uploading {Count} example packages to GitHub release", examplePackages.Count);

        foreach (var package in examplePackages)
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
            var uploadUrl =
                $"https://uploads.github.com/repos/{GitHubRepository}/releases/{releaseId}/assets?name={fileName}";
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
    /// Extracts the section for a specific version from the changelog
    /// </summary>
    [PublicAPI]
    static string ExtractVersionSection(string changelogContent, string version)
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
        Log.Information("GitHub API call: {Url}", apiUrl);
        return apiUrl;
    }
}
