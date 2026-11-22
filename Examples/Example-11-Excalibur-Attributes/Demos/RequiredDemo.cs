using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [Required] shows an error while the value is null, empty text or an empty collection.
/// <summary>Demonstrates required value validation.</summary>
public sealed class RequiredDemo
{
    /// <summary>Required display title.</summary>
    [Required] public string Title { get; set; } = "";

    /// <summary>Required tags with a custom validation message.</summary>
    [Required("Pick at least one tag")] public List<string> Tags { get; set; } = [];

    /// <summary>Required author name with a valid initial value.</summary>
    [Required] public string Author { get; set; } = "Filled in, so no error";
}
