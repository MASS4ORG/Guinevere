namespace Guinevere.Tests;

/// <summary>
/// Tests for <see cref="FocusManager"/> focus registration, request, and frame lifecycle.
/// </summary>
public class FocusManagerTests
{
    /// <summary>A control retains Tab while it claims the key and resumes navigation when it releases it.</summary>
    [Fact]
    public void TabClaimCanBeReleasedWithoutChangingTheFocusedControl()
    {
        var focus = new FocusManager();
        var input = Substitute.For<IInputHandler>();
        input.IsKeyPressed(KeyboardKey.Tab).Returns(true);
        focus.BeginFrame();
        focus.RegisterFocusableControl("editor");
        focus.RegisterFocusableControl("next");
        focus.SetClaimsTab("missing", true);
        focus.SetClaimsTab("editor", true);
        focus.RequestFocus("editor");
        focus.EndFrame();
        focus.BeginFrame();
        focus.RegisterFocusableControl("editor");
        focus.RegisterFocusableControl("next");
        focus.HandleKeyboardNavigation(input);
        focus.EndFrame();
        focus.BeginFrame();
        focus.RegisterFocusableControl("editor");
        focus.RegisterFocusableControl("next");
        Assert.Equal("editor", focus.CurrentFocusedId);

        focus.SetClaimsTab("editor", false);
        focus.HandleKeyboardNavigation(input);
        focus.EndFrame();
        focus.BeginFrame();
        Assert.Equal("next", focus.CurrentFocusedId);
    }

    [Fact]
    public void TabAndShiftTabCycleThroughRegisteredControls()
    {
        var focus = new FocusManager();
        var input = Substitute.For<IInputHandler>();
        input.IsKeyPressed(KeyboardKey.Tab).Returns(true);

        focus.BeginFrame();
        Register();
        focus.HandleKeyboardNavigation(input);
        AdvanceFrame();
        Assert.Equal("first", focus.CurrentFocusedId);

        focus.HandleKeyboardNavigation(input);
        AdvanceFrame();
        Assert.Equal("second", focus.CurrentFocusedId);

        input.IsKeyDown(KeyboardKey.LeftShift).Returns(true);
        focus.HandleKeyboardNavigation(input);
        AdvanceFrame();
        Assert.Equal("first", focus.CurrentFocusedId);

        focus.HandleKeyboardNavigation(input);
        AdvanceFrame();
        Assert.Equal("second", focus.CurrentFocusedId);

        void AdvanceFrame()
        {
            focus.EndFrame();
            focus.BeginFrame();
            Register();
        }

        void Register()
        {
            focus.RegisterFocusableControl("first");
            focus.RegisterFocusableControl("second");
        }
    }

    static Gui CreateTestGui()
    {
        var gui = new Gui();
        // Set up minimal test environment
        return gui;
    }

    /// <summary>
    /// Verifies that a new focus manager has no focused control.
    /// </summary>
    [Fact]
    public void FocusManagerInitialStateHasNoFocus()
    {
        // Arrange
        var focusManager = new FocusManager();

        // Act & Assert
        Assert.Null(focusManager.CurrentFocusedId);
        Assert.False(focusManager.HasAnyFocus);
        Assert.False(focusManager.FocusChangedThisFrame);
    }

    /// <summary>
    /// Verifies that registering a focusable control does not immediately focus it.
    /// </summary>
    [Fact]
    public void RegisterFocusableControlAddsControlToSystem()
    {
        // Arrange
        var focusManager = new FocusManager();
        focusManager.BeginFrame();

        // Act
        focusManager.RegisterFocusableControl("control1", canReceiveFocus: true);

        // Assert
        Assert.False(focusManager.HasFocus("control1")); // Not focused yet, just registered
    }

    /// <summary>
    /// Verifies that requesting focus sets the current focused id.
    /// </summary>
    [Fact]
    public void RequestFocusSetsCurrentFocusedId()
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

    /// <summary>
    /// Verifies that requesting focus on a non-focusable control does not set focus.
    /// </summary>
    [Fact]
    public void RequestFocusOnNonFocusableControlDoesNotSetFocus()
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

    /// <summary>
    /// Verifies that clearing focus removes focus from all controls.
    /// </summary>
    [Fact]
    public void ClearFocusRemovesFocusFromAllControls()
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

    /// <summary>
    /// Verifies that HasFocusWithin returns true for a parent when a child has focus.
    /// </summary>
    [Fact]
    public void HasFocusWithinWithParentChildReturnsTrueForParent()
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

    /// <summary>
    /// Verifies that controls not re-registered are removed at frame end.
    /// </summary>
    [Fact]
    public void EndFrameRemovesUnregisteredControls()
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

    /// <summary>
    /// Verifies that GetParentChain returns the correct parent hierarchy for a control.
    /// </summary>
    [Fact]
    public void GetParentChainReturnsCorrectHierarchy()
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

    /// <summary>
    /// Verifies that FocusChangedThisFrame is true only when focus actually changes.
    /// </summary>
    [Fact]
    public void FocusChangedThisFrameTrueWhenFocusChanges()
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

    /// <summary>
    /// Verifies that Gui.Focus registration and request methods work correctly.
    /// </summary>
    [Fact]
    public void GuiFocusMethodsWorkCorrectly()
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

    /// <summary>
    /// Verifies that focus can be requested with different focus reasons.
    /// </summary>
    /// <param name="reason">The focus reason to test.</param>
    [Theory]
    [InlineData(FocusReason.Mouse)]
    [InlineData(FocusReason.Keyboard)]
    [InlineData(FocusReason.Programmatic)]
    public void RequestFocusWithDifferentReasonsWorksCorrectly(FocusReason reason)
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

    /// <summary>
    /// Verifies that focus persists correctly across multiple frame cycles.
    /// </summary>
    [Fact]
    public void MultipleFrameCycleMaintainsFocusCorrectly()
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

    /// <summary>
    /// Verifies that HasFocusWithin works correctly through a deep parent hierarchy.
    /// </summary>
    [Fact]
    public void HasFocusWithinDeepHierarchyWorksCorrectly()
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

    [Fact]
    public void ActiveScopeRestrictsNavigationAndRestoresItsOpenerWhenItCloses()
    {
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("opener");
        focusManager.RequestFocus("opener");
        focusManager.EndFrame();

        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("opener");
        using (var scope = focusManager.EnterScope("popup"))
        {
            scope.SetActive();
            focusManager.RegisterFocusableControl("first");
            focusManager.RegisterFocusableControl("last");
            focusManager.Navigate(FocusDirection.Next);
        }
        focusManager.EndFrame();
        focusManager.BeginFrame();

        Assert.Equal("first", focusManager.CurrentFocusedId);
        Assert.Equal("popup", focusManager.ActiveScopeId);

        focusManager.RegisterFocusableControl("opener");
        focusManager.EndFrame();
        focusManager.BeginFrame();

        Assert.Equal("opener", focusManager.CurrentFocusedId);
        Assert.Null(focusManager.ActiveScopeId);
    }

    [Fact]
    public void NavigateUsesExplicitLinksBeforeSpatialTargets()
    {
        var focusManager = new FocusManager();
        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("left", navigationPosition: new Vector2(0, 0));
        focusManager.RegisterFocusableControl("middle", navigationPosition: new Vector2(10, 0));
        focusManager.RegisterFocusableControl("right", navigationPosition: new Vector2(20, 0));
        focusManager.SetNavigationLinks("middle", nextId: "left");
        focusManager.RequestFocus("middle");
        focusManager.EndFrame();

        focusManager.BeginFrame();
        focusManager.RegisterFocusableControl("left", navigationPosition: new Vector2(0, 0));
        focusManager.RegisterFocusableControl("middle", navigationPosition: new Vector2(10, 0));
        focusManager.RegisterFocusableControl("right", navigationPosition: new Vector2(20, 0));
        focusManager.SetNavigationLinks("middle", nextId: "left");
        focusManager.Navigate(FocusDirection.Next);
        focusManager.EndFrame();
        focusManager.BeginFrame();

        Assert.Equal("left", focusManager.CurrentFocusedId);
    }

    /// <summary>A control that uses the arrow keys keeps focus when they are pressed.</summary>
    [Fact]
    public void ArrowKeysDoNotMoveFocusAwayFromAControlThatClaimsThem()
    {
        var focus = new FocusManager();
        var input = Substitute.For<IInputHandler>();

        focus.BeginFrame();
        Register();
        focus.RequestFocus("tree");
        focus.EndFrame();
        focus.BeginFrame();
        Register();

        input.IsKeyPressed(KeyboardKey.Down).Returns(true);
        focus.HandleKeyboardNavigation(input);
        focus.EndFrame();
        focus.BeginFrame();

        Assert.Equal("tree", focus.CurrentFocusedId);

        void Register()
        {
            focus.RegisterFocusableControl("tree", new Vector2(0, 0));
            focus.SetClaimsArrowKeys("tree", true);
            focus.RegisterFocusableControl("below", new Vector2(0, 100));
        }
    }

    /// <summary>A focused text field keeps the arrow keys for moving its caret.</summary>
    [Fact]
    public void ArrowKeysDoNotMoveFocusAwayFromATextInput()
    {
        var focus = new FocusManager();
        var input = Substitute.For<IInputHandler>();

        focus.BeginFrame();
        Register();
        focus.RequestFocus("field");
        focus.EndFrame();
        focus.BeginFrame();
        Register();

        input.IsKeyPressed(KeyboardKey.Right).Returns(true);
        focus.HandleKeyboardNavigation(input);
        focus.EndFrame();
        focus.BeginFrame();

        Assert.Equal("field", focus.CurrentFocusedId);

        void Register()
        {
            focus.RegisterFocusableControl("field", new Vector2(0, 0));
            focus.RegisterTextInput("field");
            focus.RegisterFocusableControl("right", new Vector2(100, 0));
        }
    }
}
