using MASS4.Attributes;
using MASS4.Autoformers;

namespace Guinevere.Tests.Autoformers;

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
    public void HideHidesPublicMembers()
    {
        Assert.DoesNotContain(nameof(Visibility.Hidden), Names(typeof(Visibility)));
    }

    [Fact]
    public void DerivedHideAttributeHidesPublicMembers()
    {
        Assert.DoesNotContain(nameof(Visibility.Injected), Names(typeof(Visibility)));
    }

    [Fact]
    public void ShowRevealsPrivateMembers()
    {
        var names = Names(typeof(Visibility));

        Assert.Contains("_secret", names);
        Assert.Contains("SecretProperty", names);
    }

    [Fact]
    public void ShowOverridesHide()
    {
        Assert.Contains(nameof(Visibility.Both), Names(typeof(Visibility)));
    }

    [Fact]
    public void PrivateAndGetOnlyMembersWithoutShowAreHidden()
    {
        var names = Names(typeof(Visibility));

        Assert.DoesNotContain("_privateField", names);
        Assert.DoesNotContain(nameof(Visibility.GetOnly), names);
        Assert.DoesNotContain("<PublicProperty>k__BackingField", names);
    }

    [Fact]
    public void PublicReadonlyFieldsAreVisibleAndReadOnly()
    {
        var names = Names(typeof(Visibility));
        var frozen = typeof(Visibility).GetField(nameof(Visibility.Frozen))!;

        Assert.Contains(nameof(Visibility.Frozen), names);
        Assert.True(MemberMetadata.For(frozen).IsReadOnly);
        Assert.DoesNotContain(nameof(Visibility.HiddenFrozen), names);
        Assert.DoesNotContain("_privateFrozen", names);
    }

    [Fact]
    public void ConstantsAreNeverVisible()
    {
        var constant = typeof(Visibility).GetField(nameof(Visibility.Constant))!;

        Assert.False(MemberMetadata.For(constant).IsVisible);
        Assert.True(MemberMetadata.For(constant).IsReadOnly);
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
    public void SetOrderSortsMembersAndKeepsDeclarationOrderOnTies()
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
    public void BuildTitlesWithTitle()
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
    public void ButtonHonoursCanInvoke()
    {
        Assert.False(new Button("B", () => { }, () => false).IsEnabled);
        Assert.True(new Button("B", () => { }, () => true).IsEnabled);
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

    sealed class InjectedAttribute : HideAttribute;

    sealed class Visibility
    {
        public int PublicField = 0;
        public int PublicProperty { get; set; }
        [Hide] public int Hidden { get; set; }
        [Injected] public int Injected { get; set; }
        [Show, Hide] public int Both { get; set; }
        public int GetOnly => 1;
        public readonly int Frozen = 1;
        [Hide] public readonly int HiddenFrozen = 1;
        readonly int _privateFrozen = 1;
        [Show] public const int Constant = 1;
        [Show] int _secret;
        int _privateField = 0;
        [Show] int SecretProperty { get; set; }

        public int this[int index] => index;

        public void Method() => _secret = _privateField + SecretProperty + _privateFrozen;
    }

    sealed class Ordered
    {
        public int A { get; set; }
        [SetOrder(10)] public int Last { get; set; }
        public int B { get; set; }
        [SetOrder(-1)] public int First { get; set; }
    }

    sealed class Unsupported
    {
        public int Valid { get; set; }
        [Show] public int WriteOnly { set => Valid = value; }
        public Span<int> Span { get => []; set { } }
    }

    class Inherited
    {
        [Range(2, 3), SetOrder(-4)] public virtual int Ordered { get; set; }
        [Hide] public virtual int Hidden { get; set; }
    }

    sealed class Overridden : Inherited
    {
        public override int Ordered { get; set; }
        public override int Hidden { get; set; }
    }

    sealed class Titled : ITitled
    {
        public string Title => "Custom Title";
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
