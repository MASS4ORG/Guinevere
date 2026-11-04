namespace Guinevere;

/// <summary>Counts replayed commands and primitive canvas submissions, excluding opaque drawable internals.</summary>
public readonly record struct DrawStatistics
{
    /// <summary>Commands visited, including clips and callbacks.</summary>
    public int Commands { get; init; }

    /// <summary>Direct primitives visited.</summary>
    public int Primitives { get; init; }

    /// <summary>Native SKCanvas draws for direct primitives; Skia may further batch them on the GPU.</summary>
    public int PrimitiveDrawCalls { get; init; }

    /// <summary>Opaque drawable submissions whose internal draw counts are not observed.</summary>
    public int Drawables { get; init; }

    /// <summary>Custom callbacks executed.</summary>
    public int Callbacks { get; init; }

    /// <summary>Clip commands executed.</summary>
    public int Clips { get; init; }

    internal DrawStatistics Since(DrawStatistics previous) => new()
    {
        Commands = Commands - previous.Commands,
        Primitives = Primitives - previous.Primitives,
        PrimitiveDrawCalls = PrimitiveDrawCalls - previous.PrimitiveDrawCalls,
        Drawables = Drawables - previous.Drawables,
        Callbacks = Callbacks - previous.Callbacks,
        Clips = Clips - previous.Clips
    };
}
