namespace Build;

sealed partial class Build
{
    public Target GitHubCreateTag => td => td
        .DependsOn(CreateReleaseTag)
        .OnlyWhenDynamic(() => HasNewCommits && !string.IsNullOrWhiteSpace(GithubToken))
        .Executes(() => PushToRemote("GitHub", GithubToken, GithubRepository, "github.com", "x-access-token"));

    /// <summary>
    /// Creates the GitHub release for the current tag and uploads every archive in
    /// <see cref="ArtifactsDirectory"/> as a release asset.
    /// </summary>
    public Target GitHubCreateRelease => td =>
        td
            .Requires(() => GithubToken)
            .Executes(async () =>
            {
                using var http = new HttpClient();
                http.BaseAddress = new Uri("https://api.github.com/");
                http.DefaultRequestHeaders.Authorization = new("Bearer", GithubToken);
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"{ProductName}-NUKE-Build");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                var payload = JsonSerializer.Serialize(new
                {
                    tag_name = TagName,
                    name = TagName,
                    body = ReadChangelogSection(TagName) +
                        (Environment.GetEnvironmentVariable($"{ProductName.ToUpperInvariant()}_BUILD_RESULT") == "failure"
                            ? "\n\nSome platform builds failed. Downloadable assets are available only for successful builds; see the publish workflow run."
                            : string.Empty),
                    draft = false,
                    prerelease = false,
                });

                var releaseResponse = await http.PostAsync(
                    $"repos/{GithubRepository}/releases",
                    new StringContent(payload, Encoding.UTF8, "application/json"));
                releaseResponse.EnsureSuccessStatusCode();

                using var releaseDocument = JsonDocument.Parse(await releaseResponse.Content.ReadAsStringAsync());
                var releaseId = releaseDocument.RootElement.GetProperty("id").GetInt64();
                Log.Information("Created GitHub release {TagName}", TagName);

                using var uploadHttp = new HttpClient();
                uploadHttp.BaseAddress = new Uri("https://uploads.github.com/");
                uploadHttp.DefaultRequestHeaders.Authorization = new("Bearer", GithubToken);
                uploadHttp.DefaultRequestHeaders.UserAgent.ParseAdd($"{ProductName}-NUKE-Build");
                uploadHttp.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                foreach (var archive in ArtifactsDirectory.GlobFiles("*.zip", "*.deb", "*.exe"))
                {
                    using var content = new ByteArrayContent(await File.ReadAllBytesAsync(archive));
                    content.Headers.ContentType = new(archive.HasExtension("deb")
                        ? "application/vnd.debian.binary-package"
                        : archive.HasExtension("exe") ? "application/vnd.microsoft.portable-executable" : "application/zip");
                    var uploadResponse = await uploadHttp.PostAsync(
                        $"repos/{GithubRepository}/releases/{releaseId}/assets?name={Uri.EscapeDataString(archive.Name)}",
                        content);
                    uploadResponse.EnsureSuccessStatusCode();
                    Log.Information("Uploaded {Archive} to GitHub release {TagName}", archive.Name, TagName);
                }
            });
}
