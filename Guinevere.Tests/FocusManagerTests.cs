namespace Guinevere.Tests;

public class FocusManagerTests
{
    private static Gui CreateTestGui()
    {
        var gui = new Gui();
        // Set up minimal test environment
        return gui;
    }

    [Fact]
    public void FocusManager_InitialState_HasNoFocus()
    {
        // Arrange
        var focusManager = new FocusManager();

        // Act & Assert
        Assert.Null(focusManager.CurrentFocusedId);
        Assert.False(focusManager.HasAnyFocus);
        Assert.False(focusManager.FocusChangedThisFrame);
    }

    [Fact]
    public void RegisterFocusableControl_AddsControlToSystem()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();

        // Act
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);

        // Assert
        Assert.False(focusManager.HasFocus("control1")); // Not focused yet, just registered
    }

    [Fact]
    public void RequestFocus_SetsCurrentFocusedId()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);

        // Act
        focusManager.RequestFocus("control1");
        focusManager.EndFrame();
        focusManager.BeginFrame(); // Focus changes apply on next frame

        // Assert
        Assert.Equal("control1", focusManager.CurrentFocusedId);
        Assert.True(focusManager.HasAnyFocus);
        Assert.True(focusManager.HasFocus("control1"));
    }

    [Fact]
    public void RequestFocus_OnNonFocusableControl_DoesNotSetFocus()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: false);

        // Act
        focusManager.RequestFocus("control1");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert
        Assert.Null(focusManager.CurrentFocusedId);
        Assert.False(focusManager.HasAnyFocus);
    }

    [Fact]
    public void ClearFocus_RemovesFocusFromAllControls()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);
        focusManager.RequestFocus("control1");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Act
        focusManager.ClearFocus();
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert
        Assert.Null(focusManager.CurrentFocusedId);
        Assert.False(focusManager.HasAnyFocus);
    }

    [Fact]
    public void HasFocusWithin_WithParentChild_ReturnsTrueForParent()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("parent", canReceiveFocus: false);
        focusManager.RegisterFocusableControl("child", parentId: "parent", canReceiveFocus: true);

        // Act
        focusManager.RequestFocus("child");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert
        Assert.True(focusManager.HasFocus("child"));
        Assert.True(focusManager.HasFocusWithin("parent"));
        Assert.False(focusManager.HasFocus("parent")); // Parent doesn't have direct focus
    }

    [Fact]
    public void EndFrame_RemovesUnregisteredControls()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);
        focusManager.RequestFocus("control1");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Verify control has focus
        Assert.True(focusManager.HasFocus("control1"));

        // Act - Start new frame without registering control1
        focusManager.EndFrame();
        focusManager.BeginFrame();
        // Don't register control1 this frame
        focusManager.EndFrame();

        // Assert
        Assert.Null(focusManager.CurrentFocusedId);
        Assert.False(focusManager.HasAnyFocus);
    }

    [Fact]
    public void GetParentChain_ReturnsCorrectHierarchy()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("grandparent");
        focusManager.RegisterFocusableControl("parent", parentId: "grandparent");
        focusManager.RegisterFocusableControl("child", parentId: "parent");

        // Act
        var parentChain = focusManager.GetParentChain("child");

        // Assert
        Assert.Equal(2, parentChain.Count);
        Assert.Equal("parent", parentChain[0]);
        Assert.Equal("grandparent", parentChain[1]);
    }

    [Fact]
    public void FocusChangedThisFrame_TrueWhenFocusChanges()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);
        focusManager.RegisterFocusableControl("control2", canReceiveFocus: true);

        // Act 1 - Initial focus
        focusManager.RequestFocus("control1");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert 1
        Assert.True(focusManager.FocusChangedThisFrame);

        // Act 2 - No change this frame
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);
        focusManager.RegisterFocusableControl("control2", canReceiveFocus: true);
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert 2
        Assert.False(focusManager.FocusChangedThisFrame);

        // Act 3 - Change focus
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);
        focusManager.RegisterFocusableControl("control2", canReceiveFocus: true);
        focusManager.RequestFocus("control2");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert 3
        Assert.True(focusManager.FocusChangedThisFrame);
    }

    [Fact]
    public void GuiFocusMethods_WorkCorrectly()
    {
        // Arrange
        var gui = CreateTestGui();
        gui.Focus.BeginFrame();

        // Act & Assert
        Assert.NotNull(gui.Focus);
        Assert.False(gui.Focus.HasAnyFocus);

        // Test registration
        gui.Focus.RegisterFocusableControl("test-control", canReceiveFocus: true);
        gui.Focus.RequestFocus("test-control");

        gui.Focus.EndFrame();
        gui.Focus.BeginFrame();

        Assert.True(gui.Focus.HasFocus("test-control"));
        Assert.True(gui.Focus.HasAnyFocus);
    }

    [Theory]
    [InlineData(FocusReason.Mouse)]
    [InlineData(FocusReason.Keyboard)]
    [InlineData(FocusReason.Programmatic)]
    public void RequestFocus_WithDifferentReasons_WorksCorrectly(FocusReason reason)
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);

        // Act
        focusManager.RequestFocus("control1", reason);
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert
        Assert.True(focusManager.HasFocus("control1"));
    }

    [Fact]
    public void MultipleFrameCycle_MaintainsFocusCorrectly()
    {
        // Arrange
        var focusManager = new FocusManager();

        // Act & Assert - Multiple frame cycles
        for (var i = 0; i < 5; i++)
        {
            focusManager.BeginFrame();
            focusManager.RegisterFocusableControl("persistent-control", canReceiveFocus: true);

            if (i == 0)
            {
                focusManager.RequestFocus("persistent-control");
            }

            focusManager.EndFrame();
        }

        // Final check
        focusManager.BeginFrame();
        Assert.True(focusManager.HasFocus("persistent-control"));
    }

    [Fact]
    public void HasFocusWithin_DeepHierarchy_WorksCorrectly()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();

        // Create deep hierarchy: root -> level1 -> level2 -> level3 -> focused-control
        focusManager.RegisterFocusableControl("root", canReceiveFocus: false);
        focusManager.RegisterFocusableControl("level1", parentId: "root", canReceiveFocus: false);
        focusManager.RegisterFocusableControl("level2", parentId: "level1", canReceiveFocus: false);
        focusManager.RegisterFocusableControl("level3", parentId: "level2", canReceiveFocus: false);
        focusManager.RegisterFocusableControl("focused-control", parentId: "level3", canReceiveFocus: true);

        // Act
        focusManager.RequestFocus("focused-control");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        // Assert
        Assert.True(focusManager.HasFocus("focused-control"));
        Assert.True(focusManager.HasFocusWithin("level3"));
        Assert.True(focusManager.HasFocusWithin("level2"));
        Assert.True(focusManager.HasFocusWithin("level1"));
        Assert.True(focusManager.HasFocusWithin("root"));

        // Controls not in the hierarchy should not have focus
        focusManager.RegisterFocusableControl("unrelated", canReceiveFocus: true);
        Assert.False(focusManager.HasFocus("unrelated"));
        Assert.False(focusManager.HasFocusWithin("unrelated"));
    }
}
