using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Guinevere;

/// <summary>A bounded frame recording that preserves simultaneous actions without changing legacy InputScript.</summary>
public sealed class InputRecording
{
    /// <summary>Owns the ordered immutable frames.</summary>
    public InputRecording(IEnumerable<RecordedInputFrame> frames)
    {
        Frames = Array.AsReadOnly(frames.ToArray());
    }

    /// <summary>Frames in delivery order.</summary>
    public ReadOnlyCollection<RecordedInputFrame> Frames { get; }

    /// <summary>Exports a versioned recording with generated serialization metadata.</summary>
    public void Write(Stream stream) => JsonSerializer.Serialize(stream,
        new RecordingDocument(1, [.. Frames.Select(FrameDocument.From)]), RecordingJson.Default.RecordingDocument);

    /// <summary>Imports a recording with bounded input and explicit schema validation.</summary>
    public static InputRecording Read(Stream stream)
    {
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = stream.Read(block)) != 0)
        {
            if (buffer.Length + read > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("Recording exceeds the import byte limit.");
            }
            buffer.Write(block.AsSpan(0, read));
        }
        buffer.Position = 0;
        var value = JsonSerializer.Deserialize(buffer, RecordingJson.Default.RecordingDocument);
        return FromDocument(value);
    }

    static InputRecording FromDocument(RecordingDocument? value)
    {
        if (value is not { Schema: 1, Frames: not null })
        {
            throw new InvalidDataException("Invalid recording or incompatible schema.");
        }
        if (value.Frames.Any(static frame => frame is null))
        {
            throw new InvalidDataException("Recording contains a missing frame.");
        }
        return new InputRecording(value.Frames.Select(static frame => frame.ToFrame()));
    }
}

sealed record RecordingDocument(int Schema, FrameDocument[] Frames);
sealed record FrameDocument(double DeltaSeconds, int Width, int Height, float ScaleFactor,
    Vector2 Position, Vector2 Previous, float Wheel, string Text, KeyboardKey[] HeldKeys, KeyboardKey[] PressedKeys,
    MouseButton[] HeldButtons, MouseButton[] PressedButtons, GuiInputEvent[] Events, GameInputDevice[] Devices,
    GameDeviceConnection[] Connections, TouchContact[] Contacts, TouchGesture[] Gestures)
{
    internal static FrameDocument From(RecordedInputFrame frame) => new(frame.DeltaSeconds, frame.Width,
        frame.Height, frame.ScaleFactor, frame.Position, frame.Previous, frame.Wheel, frame.Text,
        [.. frame.HeldKeys], [.. frame.PressedKeys], [.. frame.HeldButtons],
        [.. frame.PressedButtons], [.. frame.Events], [.. frame.Devices],
        [.. frame.Connections], [.. frame.Contacts], [.. frame.Gestures]);

    internal RecordedInputFrame ToFrame() => new(DeltaSeconds, Width, Height, ScaleFactor,
        Position, Previous, Wheel, Text, HeldKeys, PressedKeys, HeldButtons, PressedButtons,
        Events, Devices, Connections, Contacts, Gestures);
}

[JsonSourceGenerationOptions(IncludeFields = true)]
[JsonSerializable(typeof(RecordingDocument))]
partial class RecordingJson : JsonSerializerContext;
