namespace Guinevere.Forms;

/// <summary>
/// A <see cref="FormField"/> holding a list, an array or a dictionary, seen as a sequence of
/// editable entries. Entries come back as ordinary <see cref="FormField"/>s reading and writing
/// through the index or key, so a drawer needs no separate vocabulary for what is inside a
/// collection — it recurses with the drawers it already has.
/// </summary>
public sealed class CollectionField
{
    readonly FormField source;
    readonly IList? list;
    readonly IDictionary? dictionary;
    readonly Type keyType;

    CollectionField(FormField source, IList? list, IDictionary? dictionary,
        Type keyType, Type elementType)
    {
        this.source = source;
        this.list = list;
        this.dictionary = dictionary;
        this.keyType = keyType;

        ElementType = elementType;
    }

    /// <summary>Whether entries are addressed by key rather than by position.</summary>
    public bool IsDictionary => dictionary is not null;

    /// <summary>The type each entry holds — a dictionary's value type, a list's element type.</summary>
    public Type ElementType { get; }

    /// <summary>How many entries the collection holds right now.</summary>
    public int Count => dictionary?.Count ?? list?.Count ?? 0;

    /// <summary>The field's display label, so a drawer can head the group with it.</summary>
    public string Label => source.Label;

    /// <summary>Whether entries may be written.</summary>
    public bool IsReadOnly => source.IsReadOnly || (list?.IsReadOnly ?? dictionary?.IsReadOnly ?? true);

    /// <summary>
    /// Whether entries may be added or removed. False for an array and for any dictionary whose key
    /// type offers no way to invent a fresh key.
    /// </summary>
    public bool CanResize => !IsReadOnly
                             && !(list?.IsFixedSize ?? false)
                             && (!IsDictionary || CanInventKey);

    /// <summary>
    /// Recognises a field holding a collection, or null when it holds something else. A string is a
    /// sequence but not a collection, and it has its own drawer.
    /// </summary>
    /// <param name="field">The field to classify.</param>
    /// <returns>A collection view over the field, or null.</returns>
    public static CollectionField? TryCreate(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        if (field.ValueType == typeof(string)) return null;

        return field.GetValue() switch
        {
            IDictionary map => new CollectionField(field, null, map,
                ArgumentOf(map.GetType(), typeof(IDictionary<,>), 0),
                ArgumentOf(map.GetType(), typeof(IDictionary<,>), 1)),
            IList entries => new CollectionField(field, entries, null, typeof(int),
                ArgumentOf(entries.GetType(), typeof(IList<>), 0)),
            _ => null,
        };
    }

    /// <summary>Whether <see cref="TryCreate"/> would succeed for this field.</summary>
    /// <param name="field">The field to classify.</param>
    /// <returns>True when the field holds a list, array or dictionary.</returns>
    public static bool IsCollection(FormField field) => TryCreate(field) is not null;

    /// <summary>
    /// The entries as editable fields, in index or enumeration order. Built on each call rather than
    /// cached: the collection behind them is live, and an add or a remove changes what they address.
    /// </summary>
    /// <returns>One field per entry.</returns>
    public IReadOnlyList<FormField> Entries()
    {
        if (dictionary is { } map)
        {
            var keys = Keys();
            return [.. keys.Select(key => new FormField(
                key?.ToString() ?? "null", ElementType, source.Target,
                () => map.Contains(key!) ? map[key!] : null,
                value =>
                {
                    map[key!] = value;
                    source.Touch();
                    return true;
                },
                _ => source.Touch(), IsReadOnly))];
        }

        if (list is not { } entries) return [];

        return [.. Enumerable.Range(0, entries.Count).Select(index => new FormField(
            $"[{index}]", ElementType, source.Target,
            () => index < entries.Count ? entries[index] : null,
            value =>
            {
                if (index >= entries.Count) return false;

                entries[index] = value;
                source.Touch();
                return true;
            },
            _ => source.Touch(), IsReadOnly)
        {
            CollectionMember = source.Name,
            CollectionIndex = index,
        })];
    }

    /// <summary>Appends a default entry, inventing a key when the collection is a dictionary.</summary>
    /// <returns>True when an entry was added.</returns>
    public bool Add()
    {
        if (!CanResize) return false;

        if (dictionary is { } map)
        {
            if (InventKey() is not { } key) return false;

            map[key] = Default(ElementType);
        }
        else
        {
            list!.Add(Default(ElementType));
        }

        source.Touch();
        return true;
    }

    /// <summary>Removes the entry at <paramref name="index"/> in enumeration order.</summary>
    /// <param name="index">Zero-based position among <see cref="Entries"/>.</param>
    /// <returns>True when an entry was removed.</returns>
    public bool RemoveAt(int index)
    {
        if (!CanResize || index < 0 || index >= Count) return false;

        if (dictionary is { } map) map.Remove(Keys()[index]!);
        else list!.RemoveAt(index);

        source.Touch();
        return true;
    }

    List<object?> Keys() => [.. dictionary!.Keys.Cast<object?>()];

    /// <summary>A key type a fresh entry can be invented for: text, or a whole number to count up.</summary>
    bool CanInventKey =>
        keyType == typeof(string) || keyType == typeof(int) || keyType == typeof(long);

    /// <summary>
    /// The first key not already present. A dictionary has no "append", so adding an entry from a
    /// form means choosing a placeholder the user then edits.
    /// </summary>
    object? InventKey()
    {
        var map = dictionary!;

        for (var i = 0; i < map.Count + 1; i++)
        {
            var candidate = keyType == typeof(string)
                ? (i == 0 ? "New Key" : $"New Key {i}")
                : keyType == typeof(long) ? (object)(long)i : i;

            if (!map.Contains(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>The generic argument of <paramref name="definition"/> that <paramref name="type"/> closes.</summary>
    static Type ArgumentOf(Type type, Type definition, int index) =>
        type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == definition)
            ?.GetGenericArguments()[index]
        ?? typeof(object);

    /// <summary>
    /// A new entry's starting value. Reference types other than string start empty rather than
    /// constructed: a form cannot know which constructor the owning type meant.
    /// </summary>
    static object? Default(Type type)
    {
        if (type == typeof(string)) return string.Empty;
        if (!type.IsValueType) return type.GetConstructor(Type.EmptyTypes) is null ? null : Activator.CreateInstance(type);

        return Activator.CreateInstance(type);
    }
}
