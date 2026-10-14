namespace Build;

/// <summary>
/// This is the main build file for the project.
/// It is identical in every MASS4 repository; everything project specific lives in Build.Project.cs.
/// </summary>
[ShutdownDotNetAfterServerBuild]
sealed partial class Build : NukeBuild
{
    static int Main() => Execute<Build>(static x => x.Compile);
}
