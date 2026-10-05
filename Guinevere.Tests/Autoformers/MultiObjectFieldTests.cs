using Autoformers;
using MASS4.Attributes;

namespace Guinevere.Tests.Autoformers;

/// <summary>Checks shared accessors, mixed values and projected edits independent of a rendering toolkit.</summary>
public sealed class MultiObjectFieldTests
{
    sealed class Model
    {
        [Range(0, 100)] public int Value { get; set; }
        public Vector3 Vector { get; set; }
        public string Name { get; set; } = "";
    }

    static FormField Field(Model model, string name, FormOptions? options = null) =>
        FormField.ForMember(typeof(Model).GetProperty(name)!, model, options);

    /// <summary>Shared values are live and writes notify every owner using its own policy.</summary>
    [Fact]
    public void SharedFieldsPreserveMetadataAndNotifyAllOwners()
    {
        var first = new Model { Value = 1 };
        var second = new Model { Value = 2 };
        var notified = new List<object>();
        var options = new FormOptions { MutationNotifier = notified.Add };
        var source = Field(first, nameof(Model.Value), options);
        var shared = FormField.Combine([source, Field(second, nameof(Model.Value), options)]);
        Assert.Same(source.Metadata, shared.Metadata);
        Assert.Equal((0f, 100f), shared.Range);
        Assert.True(shared.HasMixedValue);
        Assert.True(shared.SetValue(1));
        Assert.False(shared.HasMixedValue);
        Assert.Equal(1, second.Value);
        Assert.Equal([second], notified);
        second.Value = 9;
        Assert.True(shared.HasMixedValue);
        Assert.Equal(2, shared.Sources.Count);
        Assert.Same(source, FormField.Combine([source]));
        Assert.Equal(2, FormField.Combine([shared, source]).Sources.Distinct().Count());
    }

    /// <summary>Incompatible fields are rejected and any read-only owner disables the shared edit.</summary>
    [Fact]
    public void ReadOnlyAndInvalidFieldsCannotBeEdited()
    {
        var first = new Model { Value = 1 };
        var second = new Model { Value = 2 };
        var shared = FormField.Combine([Field(first, nameof(Model.Value)),
            Field(second, nameof(Model.Value), new FormOptions { ReadOnly = true })]);
        Assert.True(shared.IsReadOnly);
        Assert.False(shared.SetValue(9));
        Assert.Equal(1, first.Value);
        Assert.Equal(2, second.Value);
        Assert.Throws<ArgumentException>(() => FormField.Combine([]));
        Assert.Throws<ArgumentException>(() => FormField.Combine([Field(first, nameof(Model.Value)),
            Field(second, nameof(Model.Name))]));
        Assert.Throws<ArgumentNullException>(() => FormField.Combine(null!));
    }

    /// <summary>A projected axis writes through each owner's current vector while preserving all other axes.</summary>
    [Fact]
    public void ProjectionPreservesOtherAxesAndRejectsWrongValues()
    {
        var first = new Model { Vector = new Vector3(1, 2, 3) };
        var second = new Model { Vector = new Vector3(4, 5, 6) };
        var shared = FormField.Combine([Field(first, nameof(Model.Vector)), Field(second, nameof(Model.Vector))]);
        var x = shared.Project<Vector3, float>("X", value => value.X, (value, part) => value with { X = part });
        Assert.Equal("X", x.Label);
        Assert.Equal(typeof(float), x.ValueType);
        Assert.True(x.HasMixedValue);
        Assert.False(x.SetValue("invalid"));
        Assert.True(x.SetValue(8f));
        Assert.Equal(new Vector3(8, 2, 3), first.Vector);
        Assert.Equal(new Vector3(8, 5, 6), second.Vector);
        Assert.False(x.HasMixedValue);
    }
}
