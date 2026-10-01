using Guinevere.Forms;
using MASS4.Attributes;

namespace Guinevere.Tests.Forms;

public class FormBuilderTests
{
    [Fact]
    public void PublicReadWriteMembersAreVisible()
    {
        var names = Names(typeof(Visibility));

        Assert.Contains(nameof(Visibility.PublicField), names);
        Assert.Contains(nameof(Visibility.PublicProperty), names);
    }

    [Fact]
    public void HideInEditorHidesPublicMembers()
    {
        Assert.DoesNotContain(nameof(Visibility.Hidden), Names(typeof(Visibility)));
    }

    [Fact]
    public void DerivedHideAttributeHidesPublicMembers()
    {
        Assert.DoesNotContain(nameof(Visibility.Injected), Names(typeof(Visibility)));
    }

    [Fact]
    public void ShowInEditorRevealsPrivateMembers()
    {
        var names = Names(typeof(Visibility));

        Assert.Contains("secret", names);
        Assert.Contains("SecretProperty", names);
    }

    [Fact]
    public void ShowInEditorOverridesHideInEditor()
    {
        Assert.Contains(nameof(Visibility.Both), Names(typeof(Visibility)));
    }

    [Fact]
    public void PrivateAndGetOnlyMembersWithoutShowAreHidden()
    {
        var names = Names(typeof(Visibility));

        Assert.DoesNotContain("privateField", names);
        Assert.DoesNotContain(nameof(Visibility.GetOnly), names);
        Assert.DoesNotContain("<PublicProperty>k__BackingField", names);
    }

    [Fact]
    public void IndexersAndMethodsAreNeverMembers()
    {
        var names = Names(typeof(Visibility));

        Assert.DoesNotContain("Item", names);
        Assert.DoesNotContain(nameof(Visibility.Method), names);
    }

    [Fact]
    public void WriteOnlyAndByRefLikeMembersAreNeverMembers()
    {
        Assert.Equal([nameof(Unsupported.Valid)], Names(typeof(Unsupported)));
    }

    [Fact]
    public void OverriddenMembersKeepBaseAttributes()
    {
        var metadata = FormBuilder.EditableMetadata(typeof(Overridden));

        var ordered = Assert.Single(metadata);
        Assert.Equal(nameof(Overridden.Ordered), ordered.Member.Name);
        Assert.Equal(-4, ordered.Priority);
        Assert.NotNull(ordered.GetAttribute<RangeAttribute>());
    }

    [Fact]
    public void InspectorOrderSortsMembersAndKeepsDeclarationOrderOnTies()
    {
        Assert.Equal(["First", "A", "B", "Last"], Names(typeof(Ordered)));
    }

    [Fact]
    public void EditableMembersMatchesEditableMetadataAndIsCached()
    {
        var members = FormBuilder.EditableMembers(typeof(Ordered));

        Assert.Equal(FormBuilder.EditableMetadata(typeof(Ordered)).Select(m => m.Member), members);
        Assert.Same(members, FormBuilder.EditableMembers(typeof(Ordered)));
        Assert.Same(FormBuilder.EditableMetadata(typeof(Ordered)), FormBuilder.EditableMetadata(typeof(Ordered)));
    }

    [Fact]
    public void NullArgumentsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FormBuilder.Build(null!));
        Assert.Throws<ArgumentNullException>(() => FormBuilder.Section(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => FormBuilder.EditableMembers(null!));
        Assert.Throws<ArgumentNullException>(() => FormBuilder.EditableMetadata(null!));
    }

    [Fact]
    public void BuildTitlesWithTypeNameByDefault()
    {
        var target = new Ordered();
        var model = FormBuilder.Build(target);

        Assert.Same(target, model.Target);
        var section = Assert.Single(model.Sections);
        Assert.Equal(nameof(Ordered), section.Title);
        Assert.Same(target, section.Target);
        Assert.False(section.Removable);
    }

    [Fact]
    public void BuildTitlesWithInspectorTitle()
    {
        var model = FormBuilder.Build(new Titled());

        Assert.Equal("Custom Title", Assert.Single(model.Sections).Title);
    }

    [Fact]
    public void SectionSkipsMembersWhoseGetterThrowsWhileBuilding()
    {
        var target = new Throwing { Fail = true };
        var section = FormBuilder.Section(target, "S");

        Assert.DoesNotContain(section.Fields, field => field.Name == nameof(Throwing.Value));
        Assert.Contains(section.Fields, field => field.Name == nameof(Throwing.Fail));
    }

    [Fact]
    public void SectionKeepsTitleAndRemovable()
    {
        var section = FormBuilder.Section(new Ordered(), "Heading", removable: true);

        Assert.Equal("Heading", section.Title);
        Assert.True(section.Removable);
        Assert.Equal(4, section.Fields.Count);
    }

    [Fact]
    public void ButtonsListOnlyPublicParameterlessButtonMethods()
    {
        var section = FormBuilder.Section(new Actions(), "S");

        var button = Assert.Single(section.Buttons);
        Assert.Equal("Reset To Defaults", button.Label);
        Assert.True(button.IsEnabled);
    }

    [Fact]
    public void ButtonInvokesMethodAndNotifiesOnce()
    {
        var target = new Actions();
        var notified = new List<object>();
        var section = FormBuilder.Section(target, "S", new FormOptions { MutationNotifier = notified.Add });

        section.Buttons[0].Invoke();

        Assert.Equal(1, target.Resets);
        Assert.Equal([target], notified);
    }

    [Fact]
    public void ThrowingButtonReportsActionFailureWithoutNotifying()
    {
        var target = new Actions { Fail = true };
        var notified = new List<object>();
        var failures = new List<FormFailure>();
        var options = new FormOptions { MutationNotifier = notified.Add, FailureReporter = failures.Add };

        FormBuilder.Section(target, "S", options).Buttons[0].Invoke();

        var failure = Assert.Single(failures);
        Assert.Equal(FormFailureKind.Action, failure.Kind);
        Assert.Same(target, failure.Target);
        Assert.Equal(nameof(Actions.ResetToDefaults), failure.Member);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Empty(notified);
    }

    [Fact]
    public void ThrowingButtonWithoutReporterIsSwallowed()
    {
        var section = FormBuilder.Section(new Actions { Fail = true }, "S");

        section.Buttons[0].Invoke();
    }

    [Fact]
    public void ReadOnlyFormHasNoButtons()
    {
        var section = FormBuilder.Section(new Actions(), "S", new FormOptions { ReadOnly = true });

        Assert.Empty(section.Buttons);
    }

    [Fact]
    public void InspectorButtonHonoursCanInvoke()
    {
        Assert.False(new InspectorButton("B", () => { }, () => false).IsEnabled);
        Assert.True(new InspectorButton("B", () => { }, () => true).IsEnabled);
    }

    [Fact]
    public void EnabledFieldIsExcludedFromBodyFields()
    {
        var section = FormBuilder.Section(new Ordered(), "S");
        var enabled = section.Fields[1];

        var withSwitch = section with { EnabledField = enabled };

        Assert.Same(section.Fields, section.BodyFields);
        Assert.Equal(3, withSwitch.BodyFields.Count);
        Assert.DoesNotContain(enabled, withSwitch.BodyFields);
        Assert.Equal(4, withSwitch.Fields.Count);
    }

    [Fact]
    public void FormModelEmptyHasNoSections()
    {
        Assert.Empty(FormModel.Empty.Sections);
        Assert.NotNull(FormModel.Empty.Target);
    }

    [Fact]
    public void FormInspectionCarriesConsumerIdentity()
    {
        var target = new Ordered();
        var inspection = new FormInspection(target, FormBuilder.Build(target), "key");

        Assert.Equal("key", inspection.Key);
        Assert.Null(inspection.Context);
        Assert.Same(target, inspection.Model.Target);
    }

    static List<string> Names(Type type) => [.. FormBuilder.EditableMembers(type).Select(member => member.Name)];

    sealed class InjectedAttribute : HideInEditorAttribute;

    sealed class Visibility
    {
        public int PublicField = 0;
        public int PublicProperty { get; set; }
        [HideInEditor] public int Hidden { get; set; }
        [Injected] public int Injected { get; set; }
        [ShowInEditor, HideInEditor] public int Both { get; set; }
        public int GetOnly => 1;
        [ShowInEditor] int secret;
        int privateField = 0;
        [ShowInEditor] int SecretProperty { get; set; }

        public int this[int index] => index;

        public void Method() => secret = privateField + SecretProperty;
    }

    sealed class Ordered
    {
        public int A { get; set; }
        [InspectorOrder(10)] public int Last { get; set; }
        public int B { get; set; }
        [InspectorOrder(-1)] public int First { get; set; }
    }

    sealed class Unsupported
    {
        public int Valid { get; set; }
        [ShowInEditor] public int WriteOnly { set => Valid = value; }
        public Span<int> Span { get => []; set { } }
    }

    class Inherited
    {
        [Range(2, 3), InspectorOrder(-4)] public virtual int Ordered { get; set; }
        [HideInEditor] public virtual int Hidden { get; set; }
    }

    sealed class Overridden : Inherited
    {
        public override int Ordered { get; set; }
        public override int Hidden { get; set; }
    }

    sealed class Titled : IInspectorTitled
    {
        public string InspectorTitle => "Custom Title";
    }

    sealed class Throwing
    {
        public bool Fail { get; set; }
        public int Value
        {
            get => Fail ? throw new InvalidOperationException() : 1;
            set { }
        }
    }

    sealed class Actions
    {
        public bool Fail;
        public int Resets;

        [Button]
        public void ResetToDefaults()
        {
            if (Fail) throw new InvalidOperationException();
            Resets++;
        }

        [Button] public void WithArgument(int value) => Resets = value;

        public void NotAButton() => Resets = 0;
    }
}
