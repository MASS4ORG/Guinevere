namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the solution-wide variables.
/// </summary>
sealed partial class Build
{
    [Parameter("Config to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly string Configuration;

    string Config => Configuration ?? (IsLocalBuild ? ConfigurationOptions.Debug : ConfigurationOptions.Release);

    [Solution(GenerateProjects = true)]
    private readonly Solution Solution;

    DotNetRestoreSettings WithMsBuildProperties(DotNetRestoreSettings settings) =>
        MsBuildProperties.Aggregate(settings, (current, property) => current.SetProperty(property.Key, property.Value));

    DotNetBuildSettings WithMsBuildProperties(DotNetBuildSettings settings) =>
        MsBuildProperties.Aggregate(settings, (current, property) => current.SetProperty(property.Key, property.Value));

    DotNetRunSettings WithMsBuildProperties(DotNetRunSettings settings) =>
        MsBuildProperties.Aggregate(settings, (current, property) => current.SetProperty(property.Key, property.Value));

    DotNetPackSettings WithMsBuildProperties(DotNetPackSettings settings) =>
        MsBuildProperties.Aggregate(settings, (current, property) => current.SetProperty(property.Key, property.Value));

    DotNetPublishSettings WithMsBuildProperties(DotNetPublishSettings settings) =>
        MsBuildProperties.Aggregate(settings, (current, property) => current.SetProperty(property.Key, property.Value));
}
