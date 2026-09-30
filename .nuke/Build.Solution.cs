namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the solution-wide variables.
/// </summary>
partial class Build
{
    [Parameter(
        "Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    private readonly string Configuration;

    private string ConfigurationSet => Configuration ??
                                       (IsLocalBuild
                                           ? ConfigurationPreset.Debug
                                           : ConfigurationPreset.Release);

    [Solution(GenerateProjects = true)]
    private readonly Solution Solution;
}
