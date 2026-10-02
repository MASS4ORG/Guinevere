namespace MASS4.Autoformers;

/// <summary>
/// Type-level inspector information. Constructed once per member, independently of the inspected
/// object; live values and potentially throwing getters remain the responsibility of the form.
/// </summary>
public sealed class MemberMetadata
{
    static readonly ConditionalWeakTable<MemberInfo, Lazy<MemberMetadata>> Cache = new();

    readonly IReadOnlyList<Attribute> _attributes;
    readonly Lazy<Func<object, object?>> _read;

    MemberMetadata(MemberInfo member)
    {
        Member = member;
        ValueType = ValueTypeOf(member);
        _read = new Lazy<Func<object, object?>>(() => CompileGetter(member));
        Label = FormField.Humanize(member.Name);
        _attributes = Array.AsReadOnly(Attribute.GetCustomAttributes(member, true));
        Visibility = Select(attribute => attribute is ShowAttribute or HideAttribute);
        Layout = Select(attribute => attribute is SetOrderAttribute or ExpandAttribute);
        Validation = Select(attribute => attribute is ReadOnlyAttribute or RangeAttribute);
        RenderingHints = Select(attribute => attribute is NumericUpDownAttribute or TooltipAttribute);
        Priority = GetAttribute<SetOrderAttribute>()?.Priority ?? 0;
        IsReadOnly = GetAttribute<ReadOnlyAttribute>() is not null || !IsAssignable(member);
        IsVisible = IsShown(member);
    }

    /// <summary>Returns the shared metadata for a reflected member.</summary>
    public static MemberMetadata For(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Cache.GetValue(member, static key =>
            new Lazy<MemberMetadata>(() => new MemberMetadata(key))).Value;
    }

    /// <summary>The reflected field or property.</summary>
    public MemberInfo Member { get; }
    /// <summary>Display label.</summary>
    public string Label { get; }
    /// <summary>Declared value type.</summary>
    public Type ValueType { get; }
    /// <summary>Ordered attributes, including attributes not yet handled by the core inspector.</summary>
    public IReadOnlyList<Attribute> Attributes => _attributes;
    /// <summary>Visibility attributes.</summary>
    public IReadOnlyList<Attribute> Visibility { get; }
    /// <summary>Layout and ordering attributes.</summary>
    public IReadOnlyList<Attribute> Layout { get; }
    /// <summary>Editing constraints.</summary>
    public IReadOnlyList<Attribute> Validation { get; }
    /// <summary>Hints selecting a presentation or control.</summary>
    public IReadOnlyList<Attribute> RenderingHints { get; }
    /// <summary>Lower values appear earlier; ties preserve the original member order.</summary>
    public int Priority { get; }
    /// <summary>Whether the member should be included in the form.</summary>
    public bool IsVisible { get; }
    /// <summary>Whether editing is disabled.</summary>
    public bool IsReadOnly { get; }

    /// <summary>Reads a live value using the getter compiled when metadata was created.</summary>
    public object? GetValue(object target) => _read.Value(target);

    /// <summary>Finds the first attribute of a given type without reflecting again.</summary>
    public TAttribute? GetAttribute<TAttribute>() where TAttribute : Attribute =>
        _attributes.OfType<TAttribute>().FirstOrDefault();

    /// <summary>Finds all attributes of a given type without reflecting again.</summary>
    public IReadOnlyList<TAttribute> GetAttributes<TAttribute>() where TAttribute : Attribute =>
        [.. _attributes.OfType<TAttribute>()];

    IReadOnlyList<Attribute> Select(Func<Attribute, bool> predicate) =>
        Array.AsReadOnly([.. _attributes.Where(predicate)]);

    /// <summary>
    /// Constants never show; <c>[Show]</c> otherwise always does, and members shown by default do
    /// unless hidden.
    /// </summary>
    bool IsShown(MemberInfo member) =>
        member is not FieldInfo { IsLiteral: true }
        && ((IsShownByDefault(member) && GetAttribute<HideAttribute>() is null)
            || GetAttribute<ShowAttribute>() is not null);

    static Type ValueTypeOf(MemberInfo member) => member switch
    {
        PropertyInfo property => property.PropertyType,
        FieldInfo field => field.FieldType,
        _ => throw new ArgumentException("Expected a field or property", nameof(member))
    };

    /// <summary>
    /// A public field, read-only ones included since reading a field is cheap, or a property with a public
    /// getter and any setter. A get-only property is computed, so it is shown only on request.
    /// </summary>
    static bool IsShownByDefault(MemberInfo member) => member switch
    {
        PropertyInfo property => (property.GetMethod?.IsPublic ?? false) && property is { CanRead: true, CanWrite: true },
        FieldInfo field => field.IsPublic,
        _ => false
    };

    /// <summary>Whether the language lets the member be assigned after construction.</summary>
    static bool IsAssignable(MemberInfo member) => member switch
    {
        PropertyInfo property => property.CanWrite,
        FieldInfo field => !field.IsInitOnly && !field.IsLiteral,
        _ => false
    };

    static Func<object, object?> CompileGetter(MemberInfo member)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var instance = Expression.Convert(target, member.DeclaringType!);
        Expression access = member switch
        {
            PropertyInfo property => Expression.Property(instance, property),
            FieldInfo field => Expression.Field(instance, field),
            _ => throw new ArgumentException("Expected a field or property", nameof(member))
        };
        return Expression.Lambda<Func<object, object?>>(
            Expression.Convert(access, typeof(object)), target).Compile();
    }
}
