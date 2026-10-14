namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for packing and pushing the NuGet libraries.
/// </summary>
sealed partial class Build
{
    [Parameter("NuGet API key used by PushNuGet. Absent = pack only.")]
    [Secret]
    readonly string NuGetApiKey;

    [Parameter("NuGet feed to push to (default: nuget.org)")]
    readonly string NuGetSource = "https://api.nuget.org/v3/index.json";

    static AbsolutePath PackagesDirectory => ArtifactsDirectory / "packages";

    /// <summary>Every project declaring a PackageId. The rest of the solution is apps, tests and tooling.</summary>
    IEnumerable<Project> PackableProjects =>
        Solution.AllProjects.Where(static project =>
            project.Path.FileExists() && project.Path.ReadAllText().Contains("<PackageId>", StringComparison.Ordinal));

    public Target PackNuGet => td =>
        td
            .DependsOn(Restore, Compile) // Compile builds with --no-restore and no longer pulls in Restore itself
            .After(Publish)
            .Executes(() =>
            {
                PackagesDirectory.CreateOrCleanDirectory();

                foreach (var project in PackableProjects)
                {
                    Log.Information("Packing {Project}", project.Name);

                    _ = DotNetPack(settings =>
                    {
                        var configured = WithMsBuildProperties(settings
                            .SetProject(project)
                            .SetNoLogo(true)
                            .SetConfiguration(Config)
                            .SetOutputDirectory(PackagesDirectory)
                            .EnableNoBuild()
                            .SetVersion(Version)
                            .SetAssemblyVersion(Version)
                            .SetInformationalVersion(Version));
                        return ConfigurePackageMetadata(configured);
                    });
                }

                Log.Information("Packed {Count} packages into {Directory}",
                    PackagesDirectory.GlobFiles("*.nupkg").Count, PackagesDirectory);
            });

    /// <summary>Applies the repository's shared package metadata.</summary>
    DotNetPackSettings ConfigurePackageMetadata(DotNetPackSettings settings) =>
        settings
            .SetFileVersion(Version)
            .SetAuthors(PackageAuthors)
            .SetCopyright($"Copyright © {PackageCopyrightOwner} {DateTime.UtcNow.Year}")
            .SetPackageProjectUrl(PackageProjectUrl)
            .SetRepositoryUrl(PackageRepositoryUrl)
            .SetRepositoryType("git")
            .SetPackageRequireLicenseAcceptance(false)
            .SetPackageTags(PackageTags);

    /// <summary>
    /// Pushes with --skip-duplicate, so re-running a release for an existing version is a no-op
    /// rather than an error. Runs only when an API key is present and not gated on new commits: publishing
    /// for an existing tag must republish that exact version.
    /// </summary>
    public Target PushNuGet => td =>
        td
            .DependsOn(PackNuGet)
            .OnlyWhenStatic(() => !string.IsNullOrEmpty(NuGetApiKey))
            .Executes(() =>
            {
                var packages = PackagesDirectory.GlobFiles("*.nupkg")
                    .Where(static package => !package.Name.Contains("symbols", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (packages.Count == 0)
                {
                    Log.Warning("No packages found to publish in {Directory}", PackagesDirectory);
                    return;
                }

                EnsurePublishable(packages);

                foreach (var package in packages)
                {
                    Log.Information("Pushing {Package} to {Source}", package.Name, NuGetSource);
                    _ = DotNetNuGetPush(settings => settings
                        .SetTargetPath(package)
                        .SetSource(NuGetSource)
                        .SetApiKey(NuGetApiKey)
                        .EnableSkipDuplicate());
                }
            });

    /// <summary>
    /// Fails before the first push when any package could not be published, since nuget.org versions
    /// cannot be deleted: every ID must carry <see cref="NuGetPackagePrefix"/> and CI must build a release tag so
    /// the package version is the tagged one.
    /// </summary>
    void EnsurePublishable(IEnumerable<AbsolutePath> packages)
    {
        var unprefixed = packages
            .Select(static package => package.Name)
            .Where(static name => !name.StartsWith(NuGetPackagePrefix, StringComparison.Ordinal))
            .ToList();
        if (unprefixed.Count > 0)
        {
            throw new InvalidOperationException(
                $"Packages without the '{NuGetPackagePrefix}' prefix: {string.Join(", ", unprefixed)}");
        }

        var gitHubRef = GitHubActions.Instance?.Ref;
        if (gitHubRef != null && !gitHubRef.StartsWith("refs/tags/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Publishing must run on a release tag, not '{gitHubRef}'.");
        }
    }
}
