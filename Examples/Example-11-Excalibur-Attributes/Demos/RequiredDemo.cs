using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [Required] shows an error while the value is null, empty text or an empty collection.
public sealed class RequiredDemo
{
    [Required] public string Title { get; set; } = "";

    [Required("Pick at least one tag")] public List<string> Tags { get; set; } = [];

    [Required] public string Author { get; set; } = "Filled in, so no error";
}
