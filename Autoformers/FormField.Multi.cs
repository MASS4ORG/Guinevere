namespace Autoformers;

/// <summary>Shared fields retain each owner's accessor so an edit reaches every selected object.</summary>
public sealed partial class FormField
{
    IReadOnlyList<FormField>? sources;

    FormField(FormField source, Func<object?> read, Func<object?, bool> write, Type valueType, string label, bool? readOnly = null)
        : this(source.Name, valueType, source.Target, read, write, null, readOnly ?? source.IsReadOnly)
    {
        _metadata = source.Metadata;
        Label = label;
        Options = source.Options;
        CollectionMember = source.CollectionMember;
        CollectionIndex = source.CollectionIndex;
    }

    /// <summary>The individual accessors represented by this field, or this field for a single owner.</summary>
    public IReadOnlyList<FormField> Sources => sources ?? [this];

    /// <summary>Whether the selected owners have different values for this field.</summary>
    public bool HasMixedValue => sources is { Count: > 1 }
        && sources.Skip(1).Any(source => !Equals(sources[0].GetValue(), source.GetValue()));

    /// <summary>Combines compatible accessors, preserving metadata and refusing edits if any source is read only.</summary>
    public static FormField Combine(IEnumerable<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var all = fields.SelectMany(field => field.Sources).ToArray();
        if (all.Length == 0) throw new ArgumentException("At least one field is required.", nameof(fields));
        var first = all[0];
        if (all.Any(field => field.ValueType != first.ValueType || field.Name != first.Name))
            throw new ArgumentException("Fields must have the same name and value type.", nameof(fields));
        if (all.Length == 1) return first;
        return new FormField(first, first.GetValue, value => WriteAll(all, value), first.ValueType, first.Label, all.Any(field => field.IsReadOnly))
        {
            sources = Array.AsReadOnly(all),
        };
    }

    /// <summary>Creates an independently editable part of each value, preserving the other parts on every owner.</summary>
    public FormField Project<TValue, TPart>(string label, Func<TValue, TPart> read, Func<TValue, TPart, TValue> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        return Combine(Sources.Select(source => new FormField(source,
            () => source.GetValue() is TValue value ? read(value) : default,
            part => source.GetValue() is TValue value && part is TPart typed && source.SetValue(write(value, typed)),
            typeof(TPart), label)));
    }

    static bool WriteAll(IReadOnlyList<FormField> fields, object? value)
    {
        var success = true;
        var changed = false;
        foreach (var field in fields)
        {
            if (Equals(field.GetValue(), value)) continue;
            var written = field.SetValue(value);
            success &= written;
            changed |= written;
        }
        return success && changed;
    }
}
