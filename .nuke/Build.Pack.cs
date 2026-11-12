namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for packaging published output into release archives.
/// </summary>
sealed partial class Build
{
    // Everything the build writes lives under one folder the repository ignores; each product gets a subfolder.
    static AbsolutePath TransientDirectory => RootDirectory / ".artifacts";

    // What releases and CI upload: archives, installers, packages.
    static AbsolutePath ArtifactsDirectory => TransientDirectory / "dist";

    AbsolutePath PackedArchive =>
        ArtifactsDirectory / $"{ProductName}-{Version}-{RuntimeIdentifier}.zip";

    public Target Pack => td =>
        td
            .DependsOn(PackDependencies)
            .Executes(() =>
            {
                ArtifactsDirectory.CreateDirectory();
                PackedArchive.DeleteFile();

                // Debug symbols and the XML documentation file stay out of the release archive.
                PublishDir.ZipTo(
                    PackedArchive,
                    filter: static file => !file.HasExtension("pdb", "xml"),
                    compressionLevel: CompressionLevel.Optimal,
                    fileMode: FileMode.Create);
                Log.Information("Packed {PublishDir} into {PackedArchive}", PublishDir, PackedArchive);
            });
}
