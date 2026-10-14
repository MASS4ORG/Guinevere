namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the build process.
/// </summary>
sealed partial class Build
{
    Target Clean => td => td
        .Executes(() =>
        {
            Solution.AllProjects
                .Where(static project => project.Path != BuildProjectDirectory / "Build.csproj")
                .SelectMany(static project => new[]
                {
                    project.Directory / "bin",
                    project.Directory / "obj",
                    project.Directory / "output",
                })
                .Distinct()
                .Where(static path => path.DirectoryExists())
                .ForEach(static path => path.DeleteDirectory());
            PublishDir.DeleteDirectory();
            CoverageDirectory.DeleteDirectory();
        });

    Target Restore => td => td
        .After(Clean)
        .Executes(() =>
        {
            _ = DotNetRestore(s => WithMsBuildProperties(s.SetProjectFile(Solution)));
        });

    Target Compile => td => td
        .After(Restore)
        .Executes(() =>
        {
            Log.Debug("Config {Config}", Config);

            // A project opts out with <ExcludeFromBuild>true</ExcludeFromBuild>; then the projects are built one by one.
            var excluded = Solution.AllProjects
                .Where(static project => project.Path.FileExists()
                                  && project.Path.ReadAllText().Contains("<ExcludeFromBuild>true", StringComparison.Ordinal))
                .ToList();
            if (excluded.Count == 0)
            {
                _ = DotNetBuild(settings => WithMsBuildProperties(settings
                    .SetNoLogo(true)
                    .SetProjectFile(Solution)
                    .SetConfiguration(Config)
                    .EnableNoRestore()));
            }
            else
            {
                foreach (var project in Solution.AllProjects.Except(excluded))
                {
                    _ = DotNetBuild(settings => WithMsBuildProperties(settings
                        .SetNoLogo(true)
                        .SetProjectFile(project)
                        .SetConfiguration(Config)
                        .EnableNoRestore()));
                }
            }

            OnCompiled();
        });

    /// <summary>Runs after a successful build, for project specific follow-up steps.</summary>
    partial void OnCompiled();
}
