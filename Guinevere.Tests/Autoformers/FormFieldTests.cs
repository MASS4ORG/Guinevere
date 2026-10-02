using Autoformers;
using MASS4.Attributes;

namespace Guinevere.Tests.Autoformers;

public class FormFieldTests
{
    [Fact]
    public void FieldDescribesItsMember()
    {
        var target = new Model();
        var field = Field(target, nameof(Model.MaxResolution));

        Assert.Same(target, field.Target);
        Assert.Equal(nameof(Model.MaxResolution), field.Name);
        Assert.Equal("Max Resolution", field.Label);
        Assert.Equal(typeof(int), field.ValueType);
        Assert.NotNull(field.Metadata);
        Assert.Null(field.CollectionMember);
        Assert.Equal(-1, field.CollectionIndex);
        Assert.Equal(7, field.GetValue());
    }

    [Fact]
    public void ForMemberRejectsNullArguments()
    {
        var member = typeof(Model).GetProperty(nameof(Model.Name))!;

        Assert.Throws<ArgumentNullException>(() => FormField.ForMember(null!, new Model()));
        Assert.Throws<ArgumentNullException>(() => FormField.ForMember(member, null!));
    }

    [Fact]
    public void ReadOnlyAttributeMakesFieldReadOnly()
    {
        var target = new Model();
        var field = Field(target, nameof(Model.Locked));

        Assert.True(field.IsReadOnly);
        Assert.False(field.SetValue(5));
        Assert.Equal(1, target.Locked);
    }

    [Fact]
    public void GetOnlyPropertyIsReadOnly()
    {
        var field = Field(new Model(), nameof(Model.Computed));

        Assert.True(field.IsReadOnly);
        Assert.False(field.SetValue(3));
    }

    [Fact]
    public void ReadonlyFieldIsReadOnly()
    {
        var target = new Model();
        var field = Field(target, nameof(Model.Frozen));

        Assert.True(field.IsReadOnly);
        Assert.False(field.SetValue(9));
        Assert.Equal(1, target.Frozen);
    }

    [Fact]
    public void ReadOnlyFormMakesEveryFieldReadOnly()
    {
        var target = new Model();
        var field = Field(target, nameof(Model.Name), new FormOptions { ReadOnly = true });

        Assert.True(field.IsReadOnly);
        Assert.False(field.SetValue("changed"));
        Assert.Equal("start", target.Name);
    }

    [Fact]
    public void RangeExposesBoundsAndIsNullWithoutAttribute()
    {
        var model = new Model();

        Assert.Equal((0f, 10f), Field(model, nameof(Model.Volume)).Range);
        Assert.Null(Field(model, nameof(Model.Name)).Range);
    }

    [Fact]
    public void AttributeLookupFindsMemberAttributes()
    {
        var field = Field(new Model(), nameof(Model.Volume));

        Assert.Equal("Loudness", field.Attribute<TooltipAttribute>()?.Text);
        Assert.Null(field.Attribute<ExpandAttribute>());
    }

    [Fact]
    public void MetadataGroupsAttributesByPurpose()
    {
        var metadata = MemberMetadata.For(typeof(Model).GetField(nameof(Model.Grouped))!);

        Assert.Equal(5, metadata.Attributes.Count);
        Assert.IsType<ShowAttribute>(Assert.Single(metadata.Visibility));
        Assert.Equal(2, metadata.Layout.Count);
        Assert.IsType<ReadOnlyAttribute>(Assert.Single(metadata.Validation));
        Assert.IsType<NumericUpDownAttribute>(Assert.Single(metadata.RenderingHints));
        Assert.Equal(3, metadata.Priority);
        Assert.Single(metadata.GetAttributes<ExpandAttribute>());
        Assert.Same(metadata, MemberMetadata.For(typeof(Model).GetField(nameof(Model.Grouped))!));
    }

    [Fact]
    public void MetadataRejectsMembersOtherThanFieldsAndProperties()
    {
        var method = typeof(Model).GetMethod(nameof(ToString))!;

        Assert.Throws<ArgumentNullException>(() => MemberMetadata.For(null!));
        Assert.Throws<ArgumentException>(() => MemberMetadata.For(method));
    }

    [Fact]
    public void DisplayFieldReadsCalculatedValueAndRefusesWrites()
    {
        var target = new Model();
        var field = FormField.Display("Total", target, () => target.MaxResolution * 2);

        Assert.Equal("Total", field.Label);
        Assert.Equal(typeof(int), field.ValueType);
        Assert.Equal(14, field.GetValue());
        target.MaxResolution = 8;
        Assert.Equal(16, field.GetValue());
        Assert.True(field.IsReadOnly);
        Assert.False(field.SetValue(1));
        Assert.Null(field.Metadata);
        Assert.Null(field.Range);
        Assert.Null(field.Attribute<TooltipAttribute>());
    }

    [Fact]
    public void TouchNotifiesWithTarget()
    {
        var target = new Model();
        var notified = new List<object>();
        var field = Field(target, nameof(Model.Name), new FormOptions { MutationNotifier = notified.Add });

        field.Touch();

        Assert.Equal([target], notified);
    }

    [Fact]
    public void TouchWithoutNotifierDoesNothing()
    {
        Field(new Model(), nameof(Model.Name)).Touch();
    }

    [Theory]
    [InlineData("MaxResolution", "Max Resolution")]
    [InlineData("maxResolution", "Max Resolution")]
    [InlineData("HTTPServer", "HTTPServer")]
    [InlineData("x", "X")]
    [InlineData("", "")]
    public void HumanizeSplitsWordsAtCaseChanges(string name, string expected)
    {
        Assert.Equal(expected, FormField.Humanize(name));
    }

    [Fact]
    public void WriteStoresValueAndNotifiesOnce()
    {
        var target = new Model();
        var notified = new List<object>();
        var field = Field(target, nameof(Model.Name), new FormOptions { MutationNotifier = notified.Add });

        Assert.True(field.SetValue("changed"));

        Assert.Equal("changed", target.Name);
        Assert.Equal([target], notified);
    }

    [Fact]
    public void WriteToPublicFieldStoresValue()
    {
        var target = new Model();

        Assert.True(Field(target, nameof(Model.Grouped)).Metadata!.IsReadOnly);
        Assert.True(Field(target, nameof(Model.Count)).SetValue(4));
        Assert.Equal(4, target.Count);
    }

    [Fact]
    public void WritingEqualValueIsANoOp()
    {
        var target = new Model();
        var notified = new List<object>();
        var field = Field(target, nameof(Model.Name), new FormOptions { MutationNotifier = notified.Add });

        Assert.False(field.SetValue("start"));
        Assert.Empty(notified);
    }

    [Fact]
    public void NullIsRejectedForReferenceTypesByDefault()
    {
        var target = new Model();

        Assert.False(Field(target, nameof(Model.Name)).SetValue(null));
        Assert.Equal("start", target.Name);
    }

    [Fact]
    public void NullIsRejectedForValueTypesByDefault()
    {
        Assert.False(Field(new Model(), nameof(Model.Count)).SetValue(null));
    }

    [Fact]
    public void NullIsAcceptedForNullableValueTypesByDefault()
    {
        var target = new Model();

        Assert.True(Field(target, nameof(Model.Optional)).SetValue(null));
        Assert.Null(target.Optional);
    }

    [Fact]
    public void AcceptsNullOverridesDefaultRule()
    {
        var target = new Model();
        var allowStrings = new FormOptions { AcceptsNull = type => type == typeof(string) };
        var allowNothing = new FormOptions { AcceptsNull = _ => false };

        Assert.False(Field(target, nameof(Model.Optional), allowNothing).SetValue(null));
        Assert.True(Field(target, nameof(Model.Name), allowStrings).SetValue(null));
        Assert.Null(target.Name);
    }

    [Fact]
    public void ThrowingSetterReportsWriteFailureAndReturnsFalse()
    {
        var target = new Model();
        var notified = new List<object>();
        var failures = new List<FormFailure>();
        var options = new FormOptions { MutationNotifier = notified.Add, FailureReporter = failures.Add };

        Assert.False(Field(target, nameof(Model.Guarded), options).SetValue(-1));

        var failure = Assert.Single(failures);
        Assert.Equal(FormFailureKind.Write, failure.Kind);
        Assert.Same(target, failure.Target);
        Assert.Equal(nameof(Model.Guarded), failure.Member);
        Assert.IsType<ArgumentOutOfRangeException>(failure.Exception);
        Assert.Empty(notified);
    }

    [Fact]
    public void ThrowingGetterReportsReadFailureAndReadsNull()
    {
        var target = new Model();
        var failures = new List<FormFailure>();
        var field = Field(target, nameof(Model.Fragile), new FormOptions { FailureReporter = failures.Add });
        target.Broken = true;

        Assert.Null(field.GetValue());

        var failure = Assert.Single(failures);
        Assert.Equal(FormFailureKind.Read, failure.Kind);
        Assert.Equal(nameof(Model.Fragile), failure.Member);
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public void ThrowingGetterDuringWriteReportsReadFailure()
    {
        var target = new Model();
        var failures = new List<FormFailure>();
        var field = Field(target, nameof(Model.Fragile), new FormOptions { FailureReporter = failures.Add });
        target.Broken = true;

        Assert.False(field.SetValue(2));

        Assert.Equal(FormFailureKind.Read, Assert.Single(failures).Kind);
    }

    [Fact]
    public void FailuresWithoutReporterAreDiscarded()
    {
        var target = new Model { Broken = true };

        Assert.Null(Field(target, nameof(Model.Fragile)).GetValue());
        Assert.False(Field(target, nameof(Model.Guarded)).SetValue(-1));
    }

    static FormField Field(object target, string name, FormOptions? options = null) =>
        FormField.ForMember(target.GetType().GetMember(name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)[0], target, options);

    sealed class Model
    {
        public string? Name { get; set; } = "start";
        public int MaxResolution { get; set; } = 7;
        public int Count = 0;
        public int? Optional { get; set; } = 3;
        [ReadOnly] public int Locked { get; set; } = 1;
        public int Computed => 2;
        [Show] public readonly int Frozen = 1;
        [Range(0, 10), Tooltip("Loudness")] public float Volume { get; set; }
        [Show, SetOrder(3), Expand, ReadOnly, NumericUpDown] public int Grouped = 0;
        public bool Broken;

        public int Guarded
        {
            get => 0;
            set => ArgumentOutOfRangeException.ThrowIfNegative(value);
        }

        public int Fragile
        {
            get => Broken ? throw new InvalidOperationException() : 1;
            set { }
        }
    }
}
