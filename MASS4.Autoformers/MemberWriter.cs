namespace MASS4.Autoformers;

/// <summary>Writes a reflected member under a form's <see cref="FormOptions"/>.</summary>
static class MemberWriter
{
    /// <summary>
    /// Writes <paramref name="value"/> when it differs from the current one and the options accept it,
    /// then notifies the consumer. A throwing getter or setter is reported, not propagated.
    /// </summary>
    /// <returns>True when the member now holds the new value.</returns>
    internal static bool TrySet(MemberMetadata metadata, object target, object? value, FormOptions options)
    {
        var name = metadata.Member.Name;
        if (value is null && !options.CanAssignNull(metadata.ValueType)) return false;

        object? current;
        try { current = metadata.GetValue(target); }
        catch (Exception ex)
        {
            options.Report(FormFailureKind.Read, target, name, ex);
            return false;
        }

        if (Equals(current, value)) return false;

        try
        {
            switch (metadata.Member)
            {
                case FieldInfo field:
                    field.SetValue(target, value);
                    break;
                case PropertyInfo { CanWrite: true } property:
                    property.SetValue(target, value);
                    break;
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            options.Report(FormFailureKind.Write, target, name, ex);
            return false;
        }

        options.MutationNotifier?.Invoke(target);
        return true;
    }
}
