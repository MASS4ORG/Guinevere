using System.Text.Json;

namespace Guinevere.Profiling;

/// <summary>Writes captures as Chrome Trace Event JSON that opens in Perfetto and chrome://tracing.</summary>
public static class ChromeTraceExport
{
    /// <summary>
    /// Exports scopes, counters, diagnostics and cross-track flows. Each source becomes a process and each track a
    /// thread; timestamps are normalized per clock domain, so different domains are not aligned with each other.
    /// </summary>
    public static void Write(Stream stream, IEnumerable<ProfileCapture> captures)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(captures);
        var list = captures.OrderBy(static capture => capture.Start).ToArray();
        var origins = Origins(list);
        var processes = Processes(list);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("displayTimeUnit", "ms");
        writer.WriteStartArray("traceEvents");
        WriteMetadata(writer, list, processes);
        foreach (var capture in list)
        {
            var origin = origins[capture.Source.Clock.Domain];
            WriteFrame(new Context(writer, capture, processes[capture.Source.Id], origin));
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>Earliest capture start per clock domain, the zero point of that domain's timestamps.</summary>
    static Dictionary<Guid, long> Origins(ProfileCapture[] list) => list.GroupBy(static capture => capture.Source.Clock.Domain)
        .ToDictionary(static group => group.Key, static group => group.Min(static capture => capture.Start));

    /// <summary>Trace process id per source, numbered from 1 in order of first capture.</summary>
    static Dictionary<Guid, int> Processes(ProfileCapture[] list) => list.Select(static capture => capture.Source.Id).Distinct()
        .Select(static (id, index) => (id, index))
        .ToDictionary(static pair => pair.id, static pair => pair.index + 1);

    static void WriteMetadata(Utf8JsonWriter writer, ProfileCapture[] list, Dictionary<Guid, int> processes)
    {
        foreach (var source in list.Select(static capture => capture.Source).DistinctBy(static source => source.Id))
        {
            WriteName(writer, "process_name", processes[source.Id], 0, source.Name);
        }
        var tracks = list.SelectMany(static capture => capture.Events.Select(entry => (capture.Source.Id, entry.Track)));
        foreach (var pair in tracks.Distinct())
        {
            WriteName(writer, "thread_name", processes[pair.Id], pair.Track, $"track {pair.Track}");
        }
    }

    static void WriteName(Utf8JsonWriter writer, string kind, int process, int thread, string name)
    {
        writer.WriteStartObject();
        writer.WriteString("name", kind);
        writer.WriteString("ph", "M");
        writer.WriteNumber("pid", process);
        writer.WriteNumber("tid", thread);
        writer.WriteStartObject("args");
        writer.WriteString("name", name);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    static void WriteFrame(Context context)
    {
        WriteSpan(context, "frame", $"frame {context.Capture.Frame}", 0, context.Capture.Start, context.Capture.End);
        foreach (var entry in context.Capture.Events)
        {
            switch (entry.Kind)
            {
                case "scope":
                    WriteSpan(context, context.Capture.Source.Kind, entry.Name, entry.Track, entry.Start, entry.End);
                    break;
                case "counter":
                    WriteCounter(context, entry);
                    break;
                default:
                    WriteInstant(context, entry);
                    break;
            }
        }
        WriteFlows(context);
    }

    static void WriteSpan(Context context, string category, string name, int track, long start, long end)
    {
        var writer = context.Writer;
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("cat", category);
        writer.WriteString("ph", "X");
        writer.WriteNumber("ts", context.Micros(start));
        writer.WriteNumber("dur", context.Micros(end) - context.Micros(start));
        writer.WriteNumber("pid", context.Process);
        writer.WriteNumber("tid", track);
        writer.WriteEndObject();
    }

    static void WriteCounter(Context context, ProfileEvent entry)
    {
        var writer = context.Writer;
        writer.WriteStartObject();
        var name = string.IsNullOrEmpty(entry.Message) ? entry.Name : $"{entry.Name} ({entry.Message})";
        writer.WriteString("name", name);
        writer.WriteString("ph", "C");
        writer.WriteNumber("ts", context.Micros(entry.Start));
        writer.WriteNumber("pid", context.Process);
        writer.WriteStartObject("args");
        writer.WriteNumber("value", entry.Value ?? 0);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    static void WriteInstant(Context context, ProfileEvent entry)
    {
        var writer = context.Writer;
        writer.WriteStartObject();
        writer.WriteString("name", entry.Name);
        writer.WriteString("ph", "i");
        writer.WriteString("s", "t");
        writer.WriteNumber("ts", context.Micros(entry.Start));
        writer.WriteNumber("pid", context.Process);
        writer.WriteNumber("tid", entry.Track);
        writer.WriteStartObject("args");
        writer.WriteString("message", entry.Message);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    static void WriteFlows(Context context)
    {
        var flows = context.Capture.Events.Where(static entry => entry.Kind == "scope" && entry.FlowId != 0)
            .GroupBy(static entry => entry.FlowId);
        foreach (var flow in flows)
        {
            WriteFlow(context, [.. flow.OrderBy(static entry => entry.Start)]);
        }
    }

    /// <summary>Writes start, step and finish events; a single scope is not a flow and writes nothing.</summary>
    static void WriteFlow(Context context, ProfileEvent[] steps)
    {
        for (var i = 0; i < steps.Length && steps.Length > 1; i++)
        {
            var phase = i == 0 ? "s" : i == steps.Length - 1 ? "f" : "t";
            WriteFlowStep(context, steps[i], phase);
        }
    }

    static void WriteFlowStep(Context context, ProfileEvent entry, string phase)
    {
        var writer = context.Writer;
        writer.WriteStartObject();
        writer.WriteString("name", "flow");
        writer.WriteString("cat", "flow");
        writer.WriteString("ph", phase);
        writer.WriteString("id", $"{context.Capture.Source.Id:N}-{context.Capture.Generation}-{entry.FlowId}");
        writer.WriteString("bp", "e");
        writer.WriteNumber("ts", context.Micros(entry.Start));
        writer.WriteNumber("pid", context.Process);
        writer.WriteNumber("tid", entry.Track);
        writer.WriteEndObject();
    }

    readonly record struct Context(Utf8JsonWriter Writer, ProfileCapture Capture, int Process, long Origin)
    {
        public double Micros(long ticks) => (ticks - Origin) * 1_000_000.0 / Capture.Source.Clock.Frequency;
    }
}
