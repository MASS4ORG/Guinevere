namespace Guinevere;

/// <summary>What a <see cref="ControlsExtensions.FileDialog"/> asks the user to choose.</summary>
public enum FileDialogMode
{
    /// <summary>An existing file.</summary>
    OpenFile,

    /// <summary>A file to write to, which need not exist yet.</summary>
    SaveFile,

    /// <summary>A directory.</summary>
    SelectFolder,

    /// <summary>A directory to create, named in the dialog and not required to exist yet.</summary>
    CreateFolder,
}
