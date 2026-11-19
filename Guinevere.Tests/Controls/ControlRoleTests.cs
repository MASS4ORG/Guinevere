namespace Guinevere.Tests.Controls;

/// <summary>Roles are an open set: packages add their own, specializing a standard role.</summary>
public class ControlRoleTests
{
    static readonly ControlRole ColorWell = new("ColorWell", ControlRole.Button);

    /// <summary>A custom role is its own role and every role it specializes, nothing else.</summary>
    [Fact]
    public void CustomRoleSpecializesItsBase()
    {
        Assert.True(ColorWell.Is(ColorWell));
        Assert.True(ColorWell.Is(ControlRole.Button));
        Assert.False(ColorWell.Is(ControlRole.Checkbox));
        Assert.True(ControlRole.Switch.Is(ControlRole.Checkbox));
    }

    /// <summary>Roles compare by value, so a role rebuilt with the same name and base is the same role.</summary>
    [Fact]
    public void RolesCompareByValue()
    {
        Assert.Equal(ColorWell, new ControlRole("ColorWell", ControlRole.Button));
        Assert.NotEqual(ColorWell, new ControlRole("ColorWell"));
        Assert.Equal("ColorWell", ColorWell.ToString());
    }

    /// <summary>Activatable is inherited from the base unless the role says otherwise.</summary>
    [Fact]
    public void ActivatableIsInherited()
    {
        Assert.True(ColorWell.Activatable);
        Assert.False(new ControlRole("SearchBox", ControlRole.TextInput).Activatable);
        Assert.True(new ControlRole("CommandBox", ControlRole.TextInput, activatable: true).Activatable);
        Assert.False(ControlRole.None.Activatable);
    }

    /// <summary>A role carrying data derives from the record.</summary>
    [Fact]
    public void RolesCanCarryData()
    {
        ControlRole heading = new HeadingRole(2);
        Assert.True(heading.Is(ControlRole.Text));
        Assert.Equal(2, Assert.IsType<HeadingRole>(heading).Level);
    }

    /// <summary>An empty name is rejected.</summary>
    [Fact]
    public void EmptyNameThrows() => Assert.Throws<ArgumentException>(() => new ControlRole(" "));

    sealed record HeadingRole(int Level) : ControlRole("Heading", ControlRole.Text);
}
