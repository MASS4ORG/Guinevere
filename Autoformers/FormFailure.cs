namespace Autoformers;

/// <summary>What a form was doing when it caught an exception.</summary>
public enum FormFailureKind
{
    /// <summary>A getter threw while the value was read.</summary>
    Read,

    /// <summary>A setter threw or rejected the value.</summary>
    Write,

    /// <summary>A <c>[Button]</c> action threw.</summary>
    Action,
}

/// <summary>An exception the form caught instead of letting it escape into the GUI frame.</summary>
/// <param name="Kind">What the form was doing.</param>
/// <param name="Target">The object whose member failed.</param>
/// <param name="Member">The member or action name.</param>
/// <param name="Exception">The exception, unwrapped from reflection's invocation wrapper.</param>
public sealed record FormFailure(FormFailureKind Kind, object Target, string Member, Exception Exception);
