namespace MASS4.Autoformers;

/// <summary>
/// One editable value of an object, described without reference to any UI toolkit: what it is called,
/// what type it holds, and how to read and write it. Usually a reflected member, but a collection's
/// element is the same thing seen through an index or a key.
/// </summary>
public sealed class FormField
{
    readonly MemberMetadata? metadata;
    readonly Func<object?> read;
    readonly Func<object?, bool> write;
    readonly Action<object>? mutationNotifier;
    readonly bool forcedReadOnly;

    FormField(MemberMetadata metadata, object target, FormOptions options)
    {
        this.metadata = metadata;
        Options = options;
        mutationNotifier = options.MutationNotifier;
        forcedReadOnly = options.ReadOnly;

        Target = target;
        Name = metadata.Member.Name;
        Label = metadata.Label;
        ValueType = metadata.ValueType;

        read = () =>
        {
            try { return metadata.GetValue(target); }
            catch (Exception ex)
            {
                options.Report(FormFailureKind.Read, target, Name, ex);
                return null;
            }
        };
        write = value => MemberWriter.TrySet(metadata, target, value, options);
    }

    /// <summary>
    /// Creates a field over an accessor pair rather than a member, which is how a list entry or a
    /// dictionary value is edited: it belongs to no <see cref="MemberInfo"/>, but reads and writes
    /// exactly like one.
    /// </summary>
    internal FormField(string name, Type valueType, object target, Func<object?> read,
        Func<object?, bool> write, Action<object>? mutationNotifier, bool isReadOnly)
    {
        this.read = read;
        this.write = write;
        this.mutationNotifier = mutationNotifier;
        forcedReadOnly = isReadOnly;

        Target = target;
        Name = name;
        Label = name;
        ValueType = valueType;
    }

    /// <summary>The object this value belongs to.</summary>
    public object Target { get; }

    /// <summary>The member's name, or the element's index or key.</summary>
    public string Name { get; }

    /// <summary>For a list element, the name of the list member it belongs to; otherwise null.</summary>
    public string? CollectionMember { get; init; }

    /// <summary>For a list element, its index in <see cref="CollectionMember"/>; otherwise -1.</summary>
    public int CollectionIndex { get; init; } = -1;

    /// <summary>The policy this field was built under; entries of a collection inherit their owner's.</summary>
    public FormOptions Options { get; internal init; } = FormOptions.Default;

    /// <summary>A display label derived from the name.</summary>
    public string Label { get; }

    /// <summary>The type the value holds, which selects the drawer.</summary>
    public Type ValueType { get; }

    /// <summary>Cached metadata for a reflected member; null for collection entries.</summary>
    public MemberMetadata? Metadata => metadata;

    /// <summary>Creates a field over a reflected field or property of <paramref name="target"/>.</summary>
    /// <param name="member">The field or property.</param>
    /// <param name="target">The object holding the member.</param>
    /// <param name="options">The consumer's policy; <see cref="FormOptions.Default"/> when null.</param>
    public static FormField ForMember(MemberInfo member, object target, FormOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(target);
        return new FormField(MemberMetadata.For(member), target, options ?? FormOptions.Default);
    }

    /// <summary>Reads the current value.</summary>
    public object? GetValue() => read();

    /// <summary>Creates a calculated display field without adding a member to the inspected type.</summary>
    public static FormField Display<T>(string label, object target, Func<T> read) =>
        new(label, typeof(T), target, () => read(), _ => false, null, isReadOnly: true);

    /// <summary>
    /// Writes a value, notifying the consumer that the target changed so dirty state and any live views
    /// follow.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <returns>True if the write succeeded.</returns>
    public bool SetValue(object? value) => !IsReadOnly && write(value);

    /// <summary>
    /// Reports that the value behind this field was changed in place, for types edited through their
    /// own properties rather than by assignment — a transform, say.
    /// </summary>
    public void Touch() => mutationNotifier?.Invoke(Target);

    /// <summary>Whether the value refuses writes, from <c>[ReadOnly]</c>, a missing setter or a read-only form.</summary>
    public bool IsReadOnly =>
        forcedReadOnly
        || metadata?.IsReadOnly == true;

    /// <summary>The inclusive bounds from <c>[Range]</c>, or null when the value is unbounded.</summary>
    public (float Min, float Max)? Range =>
        Attribute<RangeAttribute>() is { } range ? (range.Min, range.Max) : null;

    /// <summary>Looks up an attribute on the member, for drawers that honour ranges, tooltips and such.</summary>
    /// <typeparam name="TAttribute">The attribute to find.</typeparam>
    public TAttribute? Attribute<TAttribute>() where TAttribute : Attribute =>
        metadata?.GetAttribute<TAttribute>();

    /// <summary>Turns <c>MaxResolution</c> into <c>Max Resolution</c>.</summary>
    internal static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var text = new StringBuilder(name.Length + 8);
        text.Append(char.ToUpperInvariant(name[0]));

        for (var i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) text.Append(' ');
            text.Append(name[i]);
        }

        return text.ToString();
    }
}
