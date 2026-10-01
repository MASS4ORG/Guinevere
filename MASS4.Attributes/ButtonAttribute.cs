namespace MASS4.Attributes;

/// <summary>
/// Marks a parameterless method to be shown in the inspector as a clickable button. Pressing it
/// invokes the method on the inspected object.
/// </summary>
/// <remarks>
/// The method must be public and take no arguments; others are skipped when the form is built.
/// The button's label is the method's name, humanized (<c>ResetToDefaults</c> reads
/// <c>Reset To Defaults</c>).
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class ButtonAttribute : Attribute;
