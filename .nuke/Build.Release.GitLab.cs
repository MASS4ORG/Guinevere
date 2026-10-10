namespace Build;

sealed partial class Build
{
    public Target GitLabCreateTag => td => td
        .DependsOn(CreateReleaseTag)
        .OnlyWhenDynamic(() => HasNewCommits && !string.IsNullOrWhiteSpace(GitlabToken))
        .Executes(() => PushToRemote("GitLab", GitlabToken, GitlabProjectPath, "gitlab.com", "oauth2"));

    /// <summary>
    /// Creates the GitLab release for the current tag: every archive in <see cref="ArtifactsDirectory"/>
    /// is uploaded to the project's generic package registry and linked from the release.
    /// </summary>
    public Target GitLabCreateRelease => td =>
        td
            .Requires(() => GitlabToken)
            .Executes(async () =>
            {
                using var http = new HttpClient();
                http.BaseAddress = new Uri("https://gitlab.com/api/v4/");
                http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", GitlabToken);

                var encodedProject = Uri.EscapeDataString(GitlabProjectPath);
                var links = new List<object>();

                foreach (var archive in ArtifactsDirectory.GlobFiles("*.zip", "*.deb", "*.exe"))
                {
                    var packageUrl = $"projects/{encodedProject}/packages/generic/{ProductName.ToLowerInvariant()}/{Version}/{archive.Name}";
                    using var content = new ByteArrayContent(await File.ReadAllBytesAsync(archive));
                    var response = await http.PutAsync(packageUrl, content);
                    response.EnsureSuccessStatusCode();

                    links.Add(new
                    {
                        name = archive.Name,
                        url = $"https://gitlab.com/api/v4/{packageUrl}",
                        direct_asset_path = $"/{archive.Name}",
                        link_type = "package",
                    });
                    Log.Information("Uploaded {Archive} to GitLab generic package registry", archive.Name);
                }

                var payload = JsonSerializer.Serialize(new
                {
                    tag_name = TagName,
                    name = TagName,
                    description = ReadChangelogSection(TagName),
                    assets = new { links },
                });

                var releaseResponse = await http.PostAsync(
                    $"projects/{encodedProject}/releases",
                    new StringContent(payload, Encoding.UTF8, "application/json"));
                releaseResponse.EnsureSuccessStatusCode();
                Log.Information("Created GitLab release {TagName}", TagName);
            });

}
