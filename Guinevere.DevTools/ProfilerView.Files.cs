using System.Text.Json;

namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    readonly FileDialogState _files;

    bool FileCommand(string name, string? key)
    {
        switch (name)
        {
            case "save-capture":
            case "open-capture":
            case "save-input":
            case "open-input":
                _files.Open(new FileDialogRequest
                {
                    Title = name.Replace('-', ' '),
                    Mode = name.StartsWith("save", StringComparison.Ordinal) ? FileDialogMode.SaveFile : FileDialogMode.OpenFile,
                    InitialName = name.EndsWith("input", StringComparison.Ordinal) ? "input.json" : "capture.json",
                    OnComplete = path => { if (path is not null) { Command("file:" + name, path); } }
                });
                return true;
            default:
                if (!name.StartsWith("file:", StringComparison.Ordinal) || key is null) { return false; }
                ApplyFile(name, key);
                return true;
        }
    }

    void ApplyFile(string command, string path)
    {
        try
        {
            switch (command)
            {
                case "file:save-capture":
                    if (Capture is null) { throw new InvalidOperationException("No capture is selected."); }
                    using (var output = File.Create(path)) { Export(output); }
                    break;
                case "file:open-capture":
                    using (var input = File.OpenRead(path)) { Import(input); }
                    break;
                case "file:save-input":
                    if (Target is not { } target) { throw new InvalidOperationException("Select a live target."); }
                    using (var output = File.Create(path)) { target.Controller.GetRecording().Write(output); }
                    break;
                case "file:open-input": LoadInput(path); break;
                default: throw new ArgumentException("Unknown file operation.", nameof(command));
            }
            Message = $"Completed {command[5..]}: {path}";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or JsonException)
        {
            Message = error.Message;
        }
    }

    void LoadInput(string path)
    {
        if (Target is not { } target) { throw new InvalidOperationException("Select a live target."); }
        if (new FileInfo(path).Length > 64 * 1024 * 1024) { throw new InvalidDataException("Input file is too large."); }
        using var input = File.OpenRead(path);
        using var document = JsonDocument.Parse(input);
        var legacy = document.RootElement.EnumerateObject().Any(static property =>
            property.Name.Equals("steps", StringComparison.OrdinalIgnoreCase));
        if (legacy)
        {
            var script = InputScript.FromJson(document.RootElement.GetRawText())
                ?? throw new InvalidDataException("InputScript is malformed.");
            target.Controller.Play(script);
        }
        else
        {
            input.Position = 0;
            target.Controller.Play(InputRecording.Read(input));
        }
        target.Controller.Paused = false;
    }
}
