namespace Autoformers;

/// <summary>An object with a composed form and a selection identity supplied by its consumer.</summary>
/// <param name="Target">The actual settings or content being inspected.</param>
/// <param name="Model">Reflected fields plus contextual sections and actions.</param>
/// <param name="Key">Identifies the selection within its context.</param>
/// <param name="Context">The consumer context that owns this selection.</param>
public sealed record FormInspection(object Target, FormModel Model, string Key, object? Context = null);
