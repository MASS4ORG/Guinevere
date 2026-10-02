namespace Autoformers;

/// <summary>
/// A consumer's policy for a form: how edits are reported, which values may be cleared and where caught
/// failures go. The form itself never persists, validates or logs.
/// </summary>
public sealed record FormOptions
{
    /// <summary>Writable options with no callbacks; null is accepted only by nullable value types.</summary>
    public static FormOptions Default { get; } = new();

    /// <summary>Called with the mutated object after any field is written or action run.</summary>
    public Action<object>? MutationNotifier { get; init; }

    /// <summary>Disables writes, collection resizing and actions throughout the form.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Whether a member of the given type may be set to null. When unset, only <see cref="Nullable{T}"/>
    /// members accept it, so a drawer's empty value never silently clears a reference.
    /// </summary>
    public Func<Type, bool>? AcceptsNull { get; init; }

    /// <summary>Receives exceptions caught from getters, setters and actions; when unset they are discarded.</summary>
    public Action<FormFailure>? FailureReporter { get; init; }

    internal bool CanAssignNull(Type type) =>
        AcceptsNull?.Invoke(type) ?? Nullable.GetUnderlyingType(type) is not null;

    internal void Report(FormFailureKind kind, object target, string member, Exception exception) =>
        FailureReporter?.Invoke(new FormFailure(kind, target, member,
            exception is TargetInvocationException { InnerException: { } inner } ? inner : exception));
}
