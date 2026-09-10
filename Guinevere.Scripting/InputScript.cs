using System.Text.Json;
using System.Text.Json.Serialization;

namespace Guinevere;

/// <summary>
/// A recorded sequence of pointer and keyboard actions, played back against a real frame loop by
/// <see cref="InputScriptPlayer"/>. Lets pointer-driven behaviour be tested without a display.
/// </summary>
/// <example>
/// <code>
/// {
///   "width": 1400,
///   "height": 900,
///   "steps": [
///     { "action": "move", "x": 40, "y": 59 },
///     { "action": "press", "button": "left" },
///     { "action": "move", "x": 1220, "y": 300 },
///     { "action": "dump", "path": "/tmp/mid-drag.png" },
///     { "action": "release", "button": "left" },
///     { "action": "expectCapture", "value": "none" }
///   ]
/// }
/// </code>
/// </example>
public sealed record InputScript
{
    /// <summary>Canvas width the script is written against.</summary>
    public int Width { get; init; } = 1600;

    /// <summary>Canvas height the script is written against.</summary>
    public int Height { get; init; } = 950;

    /// <summary>Free-text note, ignored during playback.</summary>
    public string? Comment { get; init; }

    /// <summary>The actions, played in order. Every one advances at least one frame.</summary>
    public IReadOnlyList<InputScriptStep> Steps { get; init; } = [];

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    /// <summary>Reads a script. Returns null if the JSON is malformed.</summary>
    public static InputScript? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<InputScript>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes the script back out.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Options);
}
