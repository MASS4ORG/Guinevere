namespace Guinevere.Tests.Examples;

/// <summary>Checks that the control gallery's badge ships with its assembly.</summary>
public class ControlGalleryTests
{
    /// <summary>Loads the real embedded badge and reuses its decoded image across frames.</summary>
    [Fact]
    public void Badge_LoadsFromAssemblyAndIsCached()
    {
        var badge = Controls_01.Program.LoadBadge();

        Assert.Equal(1024, badge.Width);
        Assert.Equal(1024, badge.Height);
        Assert.Same(badge, Controls_01.Program.LoadBadge());
    }
}
