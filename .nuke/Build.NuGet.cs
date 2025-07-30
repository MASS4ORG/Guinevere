using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace Guinevere.Nuke;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for creating and publishing NuGet packages.
/// </summary>
partial class Build
{
    [Parameter("NuGet API Key for publishing packages")] public readonly string NuGetApiKey;

    [Parameter("NuGet source URL (default: nuget.org)")]
    public readonly string NuGetSource = "https://api.nuget.org/v3/index.json";

    [Parameter("Skip NuGet symbol packages")] public readonly bool SkipSymbols;

    private static AbsolutePath PackagesDirectory => RootDirectory / "packages";

    /// <summary>
    /// Gets the list of projects that should be packaged as NuGet packages
    /// </summary>
    private List<Project> PackageableProjects => Solution.AllProjects
        .Where(ShouldCreateNuGetPackage)
        .ToList();

    /// <summary>
    /// Determines if a project should be packaged as a NuGet package
    /// </summary>
    private static bool ShouldCreateNuGetPackage(Project project)
    {
        try
        {
            var projectFile = project.Path;
            if (!projectFile.Exists()) return false;

            var content = projectFile.ReadAllText();

            // Check for explicit packaging properties
            if (content.Contains("<IsPackable>true</IsPackable>", StringComparison.OrdinalIgnoreCase))
                return true;

            if (content.Contains("<IsPackable>false</IsPackable>", StringComparison.OrdinalIgnoreCase))
                return false;

            // Check for library projects (exclude executables, tests, etc.)
            var isLibrary = content.Contains("<OutputType>Library</OutputType>", StringComparison.OrdinalIgnoreCase) ||
                            (!content.Contains("<OutputType>", StringComparison.OrdinalIgnoreCase) &&
                             !project.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) &&
                             !project.Name.Contains("Sample", StringComparison.OrdinalIgnoreCase) &&
                             !project.Name.Contains("Example", StringComparison.OrdinalIgnoreCase));

            return isLibrary;
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to analyze project {Project}: {Error}", project.Name, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates NuGet packages for all library projects
    /// </summary>
    private Target PackNuGet => td => td
        .DependsOn(Restore, Compile)
        .After(Publish)
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {
            PackagesDirectory.CreateDirectory();

            foreach (var project in PackageableProjects)
            {
                Log.Information("Creating NuGet package for {Project}", project.Name);

                _ = DotNetTasks.DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(ConfigurationSet)
                    .SetOutputDirectory(PackagesDirectory)
                    .SetVersion(VersionFull)
                    .SetAssemblyVersion(VersionFull)
                    .SetFileVersion(VersionFull)
                    .SetInformationalVersion(VersionFull)
                    .SetPackageId($"org.Mass4.{project.Name}")
                    .SetTitle(GetPackageTitle(project))
                    .SetAuthors("Bruno Massa")
                    .SetCopyright($"Copyright © Bruno Massa {DateTime.UtcNow.Year}")
                    .SetPackageProjectUrl("https://mass4.org/guinevere")
                    .SetRepositoryUrl("https://github.com/mass4org/guinevere.git")
                    .SetRepositoryType("git")
                    .SetPackageRequireLicenseAcceptance(false)
                    .SetPackageTags("gui imgui graphics opengl vulkan skia gamedev opentk raylib silk.net")
                    .SetPackageIconUrl("icon.png")
                    // .SetDescription(GetPackageDescription(project))
                    // .SetPackageReadmeFile("README.md")
                    // .EnableIncludeSymbols(!SkipSymbols)
                    // .EnableIncludeSource(!SkipSymbols)
                    .EnableNoBuild()
                );
            }

            Log.Information("Successfully created {Count} NuGet packages in {Directory}",
                PackageableProjects.Count, PackagesDirectory);
        });

    /// <summary>
    /// Publishes NuGet packages to the configured source
    /// </summary>
    private Target PublishNuGet => td => td
        .DependsOn(PackNuGet)
        .OnlyWhenStatic(() => !string.IsNullOrEmpty(NuGetApiKey))
        .OnlyWhenStatic(() => HasNewCommits)
        .Executes(() =>
        {
            var packages = PackagesDirectory.GlobFiles("*.nupkg")
                .Where(p => !p.Name.Contains("symbols", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!packages.Any())
            {
                Log.Warning("No packages found to publish in {Directory}", PackagesDirectory);
                return;
            }

            foreach (var package in packages)
            {
                Log.Information("Publishing package {Package} to {Source}", package.Name, NuGetSource);

                _ = DotNetTasks.DotNetNuGetPush(s => s
                    .SetTargetPath(package)
                    .SetSource(NuGetSource)
                    .SetApiKey(NuGetApiKey)
                    .SetSkipDuplicate(true)
                );
            }

            Log.Information("Successfully published {Count} packages to {Source}", packages.Count, NuGetSource);
        });

    /// <summary>
    /// Gets the NuGet package title for a project
    /// </summary>
    private static string GetPackageTitle(Project project) =>
        project.Name switch
        {
            "Guinevere" => "Guinevere - GPU Accelerated IM GUI System",
            "Guinevere.OpenGL.OpenTK" => "Guinevere OpenGL Integration for OpenTK",
            "Guinevere.OpenGL.Raylib" => "Guinevere OpenGL Integration for Raylib",
            "Guinevere.OpenGL.SilkNET" => "Guinevere OpenGL Integration for Silk.NET",
            "Guinevere.Vulkan.SilkNET" => "Guinevere Vulkan Integration for Silk.NET",
            _ => $"Guinevere - {project.Name}"
        };

    /// <summary>
    /// Gets the NuGet package description for a project
    /// </summary>
    private static string GetPackageDescription(Project project) =>
        project.Name switch
        {
            "Guinevere" =>
                "A GPU accelerated immediate mode GUI system built on SkiaSharp. Provides high-performance rendering with modern graphics APIs support.",
            "Guinevere.OpenGL.OpenTK" =>
                "OpenGL integration package for Guinevere GUI system using OpenTK. Enables GPU-accelerated rendering through OpenGL.",
            "Guinevere.OpenGL.Raylib" =>
                "OpenGL integration package for Guinevere GUI system using Raylib. Provides easy integration with Raylib-based applications.",
            "Guinevere.OpenGL.SilkNET" =>
                "OpenGL integration package for Guinevere GUI system using Silk.NET. Modern OpenGL bindings for high-performance applications.",
            "Guinevere.Vulkan.SilkNET" =>
                "Vulkan integration package for Guinevere GUI system using Silk.NET. Next-generation graphics API support for maximum performance.",
            _ => $"Integration package for Guinevere GUI system - {project.Name}"
        };
}
