using System.Text.Json;
using System.Text.Json.Serialization;

namespace Guinevere.Profiling;

/// <summary>Reads and writes bounded, versioned JSON captures using generated AOT-safe metadata.</summary>
public static class ProfileExport
{
    /// <summary>Provisional additive schema version; unknown optional JSON properties are ignored.</summary>
    public const int Schema = 1;
    /// <summary>Maximum JSON input accepted before deserialization.</summary>
    public const int MaximumBytes = 64 * 1024 * 1024;

    /// <summary>Exports an owned capture without retaining live provider state.</summary>
    public static void Write(Stream stream, ProfileCapture capture)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(capture);
        var document = new CaptureDocument(Schema, capture.Session, capture.Source, capture.Frame,
            capture.Generation, capture.Start, capture.End, [.. capture.Events], [.. capture.Items],
            capture.Dropped, capture.RetainedBytes);
        JsonSerializer.Serialize(stream, document, CaptureJson.Default.CaptureDocument);
    }

    /// <summary>Imports a bounded capture; incompatible versions and invalid required values fail explicitly.</summary>
    public static ProfileCapture Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = stream.Read(block)) != 0)
        {
            if (buffer.Length + read > MaximumBytes)
            {
                throw new InvalidDataException("Capture exceeds the import byte limit.");
            }
            buffer.Write(block.AsSpan(0, read));
        }
        buffer.Position = 0;
        var document = JsonSerializer.Deserialize(buffer, CaptureJson.Default.CaptureDocument)
            ?? throw new InvalidDataException("Capture is empty.");
        Validate(document);
        return new ProfileCapture(document.Session, document.Source, document.Frame, document.Generation,
            document.Start, document.End, document.Events, document.Items, document.Dropped, document.RetainedBytes);
    }

    static void Validate(CaptureDocument value)
    {
        ValidateHeader(value);
        ValidatePayload(value);
        ValidateSource(value.Source);
        ValidateEvents(value);
        ValidateItems(value.Items);
    }

    static void ValidateHeader(CaptureDocument value)
    {
        if (value.Schema != Schema || value.Session == Guid.Empty || value.Generation <= 0)
        {
            throw new InvalidDataException("Invalid capture header or incompatible schema.");
        }
    }

    static void ValidatePayload(CaptureDocument value)
    {
        if (value.End < value.Start || value.Dropped < 0 || value.RetainedBytes < 0
            || value.Events is null || value.Items is null)
        {
            throw new InvalidDataException("Invalid capture payload.");
        }
    }

    static void ValidateSource(ProfileSourceInfo? source)
    {
        if (source is null || source.Id == Guid.Empty || string.IsNullOrWhiteSpace(source.Name)
            || string.IsNullOrWhiteSpace(source.Kind) || source.Clock is null)
        {
            throw new InvalidDataException("Invalid capture source.");
        }
        ValidateClock(source.Clock);
    }

    static void ValidateClock(ProfileClockInfo clock)
    {
        if (clock.Domain == Guid.Empty || clock.Frequency <= 0 || clock.ResolutionTicks is <= 0)
        {
            throw new InvalidDataException("Invalid capture clock.");
        }
    }

    static void ValidateEvents(CaptureDocument value)
    {
        var ids = new HashSet<long>();
        foreach (var entry in value.Events)
        {
            ValidateEvent(entry);
            ValidateNumber(entry.Value);
            if (!ids.Add(entry.Id) || entry.Start < value.Start || entry.End > value.End)
            {
                throw new InvalidDataException("Invalid capture event.");
            }
        }
    }

    static void ValidateEvent(ProfileEvent entry)
    {
        if (entry.Id <= 0 || entry.End < entry.Start || entry.Name is null
            || entry.Kind is not ("scope" or "counter" or "diagnostic"))
        {
            throw new InvalidDataException("Invalid capture event.");
        }
    }

    static void ValidateNumber(double? value)
    {
        if (value is { } number && !double.IsFinite(number))
        {
            throw new InvalidDataException("Capture number exceeds the finite measurement range.");
        }
    }
    static void ValidateItems(ProfileItem[] items)
    {
        foreach (var item in items)
        {
            ValidateItemText(item);
            if (!Finite(item.X) || !Finite(item.Y) || !Finite(item.Width) || !Finite(item.Height))
            {
                throw new InvalidDataException("Invalid capture inspection value.");
            }
        }
    }
    static void ValidateItemText(ProfileItem item)
    {
        if (item.Id is null || item.ParentId is null || item.Category is null || item.Name is null || item.Value is null)
        {
            throw new InvalidDataException("Invalid capture inspection text.");
        }
    }

    static bool Finite(float? value) => value is null || float.IsFinite(value.Value);
}

sealed record CaptureDocument(int Schema, Guid Session, ProfileSourceInfo Source, long Frame,
    long Generation, long Start, long End, ProfileEvent[] Events, ProfileItem[] Items, long Dropped, long RetainedBytes);

[JsonSerializable(typeof(CaptureDocument))]
partial class CaptureJson : JsonSerializerContext;
