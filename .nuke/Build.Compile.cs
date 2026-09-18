using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;

namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the build process for libraries and integrations.
/// </summary>
partial class Build
{
    private Target Clean => s => s
        .Executes(() =>
        {
            var nukeBuildDir = Solution.Build.Directory;

            Solution.Directory.GlobDirectories("**/bin", "**/obj", "**/output")
                .Where(path => !path.ToString().StartsWith(nukeBuildDir))
                .ForEach(path => path.DeleteDirectory());

            PublishDir.DeleteDirectory();
            CoverageDirectory.DeleteDirectory();
        });

    private Target Restore => td => td
        .After(Clean)
        .Executes(() =>
        {
            _ = DotNetTasks.DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    private Target Compile => td => td
        .After(Restore)
        .Executes(() =>
        {
            Log.Debug("Configuration {Configuration}", ConfigurationSet);
            Log.Debug("configuration {configuration}", Configuration);

            var projectsToBuild = Solution.AllProjects
                .Where(p => p.GetProperty("ExcludeFromBuild")?.Equals("true", StringComparison.OrdinalIgnoreCase) != true);

            foreach (var project in projectsToBuild)
            {
                _ = DotNetTasks.DotNetBuild(s => s
                    .SetProjectFile(project)
                    .SetConfiguration(ConfigurationSet)
                    .EnableNoRestore()
                );
            }
        });
}
