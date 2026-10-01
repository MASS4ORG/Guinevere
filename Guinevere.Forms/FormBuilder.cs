namespace Guinevere.Forms;

/// <summary>
/// Turns an object into a <see cref="FormModel"/> by reflection. The member list is cached per type,
/// because an inspector rebuilds this many times a second.
/// </summary>
public static class FormBuilder
{
    const BindingFlags memberScope =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    static readonly ConditionalWeakTable<Type, Lazy<IReadOnlyList<MemberMetadata>>> MembersByType = new();
    static readonly ConditionalWeakTable<Type, Lazy<IReadOnlyList<MemberInfo>>> MemberViewsByType = new();
    static readonly ConditionalWeakTable<Type, Lazy<MethodInfo[]>> ButtonMethodsByType = new();

    /// <summary>Builds the form for a plain object: one section holding its editable members.</summary>
    /// <param name="target">The object to inspect.</param>
    /// <param name="options">The consumer's policy; <see cref="FormOptions.Default"/> when null.</param>
    public static FormModel Build(object target, FormOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        var title = target is ITitled titled ? titled.Title : target.GetType().Name;
        return new FormModel(target, [Section(target, title, options)]);
    }

    /// <summary>
    /// Builds one section over an object's editable members and <c>[Button]</c> methods, for consumers
    /// composing several objects into one form.
    /// </summary>
    /// <param name="target">The object to inspect.</param>
    /// <param name="title">The section heading.</param>
    /// <param name="options">The consumer's policy; <see cref="FormOptions.Default"/> when null.</param>
    /// <param name="removable">Whether the shell may offer to remove the target.</param>
    public static FormSection Section(object target, string title, FormOptions? options = null, bool removable = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        options ??= FormOptions.Default;

        var fields = EditableMetadata(target.GetType())
            .Where(metadata => CanReadSafely(metadata, target))
            .Select(metadata => FormField.ForMember(metadata.Member, target, options))
            .ToList();

        var buttons = options.ReadOnly ? [] : ButtonsFor(target, options);
        return new FormSection(title, target, fields, removable) { Buttons = buttons };
    }

    /// <summary>The editable members of a type, in inspector order.</summary>
    /// <param name="type">The type to reflect over.</param>
    public static IReadOnlyList<MemberInfo> EditableMembers(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return MemberViewsByType.GetValue(type, static t =>
            new Lazy<IReadOnlyList<MemberInfo>>(() =>
                Array.AsReadOnly([.. EditableMetadata(t).Select(metadata => metadata.Member)]))).Value;
    }

    /// <summary>Cached, ordered metadata for the visible members of a type.</summary>
    public static IReadOnlyList<MemberMetadata> EditableMetadata(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return MembersByType.GetValue(type, static t =>
            new Lazy<IReadOnlyList<MemberMetadata>>(() =>
                Array.AsReadOnly([
                    .. t.GetMembers(memberScope)
                        .Where(member => member is FieldInfo { FieldType.IsByRefLike: false } || member is PropertyInfo
                            {
                                GetMethod: not null
                            } property
                            && property.GetIndexParameters().Length == 0
                            && !property.PropertyType.IsByRefLike)
                        .Select(MemberMetadata.For)
                        .Where(metadata => metadata.IsVisible)
                        .OrderBy(metadata => metadata.Priority)
                ]))).Value;
    }

    static List<Button> ButtonsFor(object target, FormOptions options) =>
        [.. ButtonMethodsByType.GetValue(target.GetType(), static t =>
                new Lazy<MethodInfo[]>(() => [.. t
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.GetCustomAttribute<ButtonAttribute>() is not null)
                    .Where(method => method.GetParameters().Length == 0)])).Value
            .Select(method => new Button(FormField.Humanize(method.Name),
                () =>
                {
                    try
                    {
                        method.Invoke(target, null);
                        options.MutationNotifier?.Invoke(target);
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException is not null)
                    {
                        options.Report(FormFailureKind.Action, target, method.Name, ex);
                    }
                }))];

    /// <summary>A property getter can throw on a half-built object; such members are skipped.</summary>
    static bool CanReadSafely(MemberMetadata metadata, object target)
    {
        if (metadata.Member is not PropertyInfo) return true;

        try
        {
            _ = metadata.GetValue(target);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
