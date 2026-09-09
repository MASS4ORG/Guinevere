namespace Guinevere.Tests.Animation;

/// <summary>
/// Tests for animation functionality on the Gui class.
/// </summary>
public class GuiAnimationTests
{
    private Gui CreateTestGui()
    {
        var gui = new Gui();
        return gui;
    }

    /// <summary>
    /// Verifies that GetAnimationFloat returns a new instance each time.
    /// </summary>
    [Fact]
    public void GetAnimationFloat_ReturnsNewInstance()
    {
        // Arrange
        var gui = CreateTestGui();
        var initialValue = 5.0f;

        // Act
        var animation1 = gui.GetAnimationFloat(initialValue);
        var animation2 = gui.GetAnimationFloat(initialValue);

        // Assert
        Assert.NotSame(animation1, animation2); // Should be different instances
        Assert.Equal(initialValue, animation1.GetValue());
        Assert.Equal(initialValue, animation2.GetValue());
    }

    /// <summary>
    /// Verifies that GetAnimationFloat with no arguments returns a zero initial value.
    /// </summary>
    [Fact]
    public void GetAnimationFloat_DefaultValue_ReturnsZero()
    {
        // Arrange
        var gui = CreateTestGui();

        // Act
        var animation = gui.GetAnimationFloat();

        // Assert
        Assert.Equal(0f, animation.GetValue());
    }

    /// <summary>
    /// Verifies that AnimateBool01 from the same location returns the same animation.
    /// </summary>
    [Fact]
    public void AnimateBool01_SameLocation_ReturnsSameAnimation()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Act - Call from exact same location (same line)
        var result1 = CallAnimateBool01FromSameLocation(gui);
        var result2 = CallAnimateBool01FromSameLocation(gui);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(1, gui.ActiveAnimationCount);
    }

    private float CallAnimateBool01FromSameLocation(Gui gui) => gui.AnimateBool01(true, 0.5f, Easing.Linear);

    /// <summary>
    /// Verifies that the initial state call returns the target value directly.
    /// </summary>
    [Fact]
    public void AnimateBool01_InitialState_ReturnsTargetValue()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Act
        var resultTrue = gui.AnimateBool01(true, 0.5f, Easing.Linear);
        var resultFalse = gui.AnimateBool01(false, 0.5f, Easing.Linear);

        // Assert
        Assert.Equal(1.0f, resultTrue);
        Assert.Equal(0.0f, resultFalse);
    }

    /// <summary>
    /// Verifies that a state change triggers an animation on the Gui class.
    /// </summary>
    [Fact]
    public void AnimateBool01_StateChange_TriggersAnimation()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Start with false
        var initialResult = CallAnimateBool01StateChange1(gui, false, 0.5f);

        // Act - Change to true
        Thread.Sleep(50);
        // gui.Time.Update();
        var animatingResult = CallAnimateBool01StateChange1(gui, true, 0.1f);

        // Assert
        Assert.Equal(0.0f, initialResult);
        Assert.True(animatingResult >= 0.0f && animatingResult <= 1.0f);
        Assert.Equal(1, gui.RunningAnimationCount);
    }

    private float CallAnimateBool01StateChange1(Gui gui, bool state, float duration) =>
        gui.AnimateBool01(state, duration, Easing.Linear);

    /// <summary>
    /// Verifies that ActiveAnimationCount reflects the total number of managed animations.
    /// </summary>
    [Fact]
    public void ActiveAnimationCount_ReflectsNumberOfManagedAnimations()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Act
        gui.AnimateBool01(true, 0.5f, Easing.Linear);
        gui.AnimateBool01(false, 0.5f, Easing.Linear);
        gui.AnimateBool01(true, 0.5f, Easing.Linear);

        // Assert
        Assert.Equal(3, gui.ActiveAnimationCount);
    }

    /// <summary>
    /// Verifies that RunningAnimationCount reflects only actively animating entries.
    /// </summary>
    [Fact]
    public void RunningAnimationCount_ReflectsActivelyAnimatingCount()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Create animations in steady state
        CallAnimateBool01Running1(gui, true, 0.5f);
        CallAnimateBool01Running2(gui, false, 0.5f);
        var runningBefore = gui.RunningAnimationCount;

        // Act - Trigger animations
        Thread.Sleep(50);
        // gui.Time.Update();
        CallAnimateBool01Running1(gui, false, 0.2f); // Change first animation
        CallAnimateBool01Running2(gui, true, 0.2f); // Change second animation
        var runningAfter = gui.RunningAnimationCount;

        // Assert
        Assert.Equal(0, runningBefore);
        Assert.Equal(2, runningAfter);
    }

    private void CallAnimateBool01Running1(Gui gui, bool state, float duration) =>
        gui.AnimateBool01(state, duration, Easing.Linear);

    private void CallAnimateBool01Running2(Gui gui, bool state, float duration) =>
        gui.AnimateBool01(state, duration, Easing.Linear);

    /// <summary>
    /// Verifies that ClearAnimations removes all animations from the Gui.
    /// </summary>
    [Fact]
    public void ClearAnimations_RemovesAllAnimations()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Add some animations
        gui.AnimateBool01(true, 0.5f, Easing.Linear);
        gui.AnimateBool01(false, 0.5f, Easing.Linear);
        gui.AnimateBool01(true, 0.5f, Easing.Linear);

        // Act
        gui.ClearAnimations();

        // Assert
        Assert.Equal(0, gui.ActiveAnimationCount);
        Assert.Equal(0, gui.RunningAnimationCount);
    }

    /// <summary>
    /// Verifies that different easing functions work correctly with AnimateBool01.
    /// </summary>
    [Fact]
    public void AnimateBool01_DifferentEasingFunctions_WorkCorrectly()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Act
        var linear = CallAnimateBool01Easing1(gui, false, 0.5f, Easing.Linear);
        var easeIn = CallAnimateBool01Easing2(gui, false, 0.5f, Easing.EaseIn);
        var smoothStep = CallAnimateBool01Easing3(gui, false, 0.5f, Easing.SmoothStep);

        Thread.Sleep(50);
        // gui.Time.Update();

        var linearAnimating = CallAnimateBool01Easing1(gui, true, 0.2f, Easing.Linear);
        var easeInAnimating = CallAnimateBool01Easing2(gui, true, 0.2f, Easing.EaseIn);
        var smoothStepAnimating = CallAnimateBool01Easing3(gui, true, 0.2f, Easing.SmoothStep);

        // Assert
        Assert.Equal(0.0f, linear);
        Assert.Equal(0.0f, easeIn);
        Assert.Equal(0.0f, smoothStep);

        // All should be animating (with some tolerance for timing)
        Assert.True(linearAnimating >= 0.0f);
        Assert.True(easeInAnimating >= 0.0f);
        Assert.True(smoothStepAnimating >= 0.0f);

        Assert.Equal(3, gui.ActiveAnimationCount);
        Assert.Equal(3, gui.RunningAnimationCount);
    }

    private float CallAnimateBool01Easing1(Gui gui, bool state, float duration, Func<float, float> easing) =>
        gui.AnimateBool01(state, duration, easing);

    private float CallAnimateBool01Easing2(Gui gui, bool state, float duration, Func<float, float> easing) =>
        gui.AnimateBool01(state, duration, easing);

    private float CallAnimateBool01Easing3(Gui gui, bool state, float duration, Func<float, float> easing) =>
        gui.AnimateBool01(state, duration, easing);

    /// <summary>
    /// Verifies that a zero-duration animation sets the value immediately without animating.
    /// </summary>
    [Fact]
    public void AnimateBool01_ZeroDuration_NoAnimation()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Act
        var result1 = gui.AnimateBool01(false, 0.0f, Easing.Linear);
        var result2 = gui.AnimateBool01(true, 0.0f, Easing.Linear);

        // Assert
        Assert.Equal(0.0f, result1);
        Assert.Equal(1.0f, result2);
        Assert.Equal(0, gui.RunningAnimationCount);
    }

    /// <summary>
    /// Verifies that calls from different caller locations create separate animations.
    /// </summary>
    [Fact]
    public void AnimateBool01_CallsFromDifferentLocations_CreateSeparateAnimations()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        // Act - These calls should be treated as different locations due to line numbers
        CallAnimateBool01Line1(gui);
        CallAnimateBool01Line2(gui);

        // Assert
        Assert.Equal(2, gui.ActiveAnimationCount);
    }

    // Helper methods to test different caller locations
    private void CallAnimateBool01Line1(Gui gui) => gui.AnimateBool01(true, 0.5f, Easing.Linear);
    private void CallAnimateBool01Line2(Gui gui) => gui.AnimateBool01(false, 0.5f, Easing.Linear);

    /// <summary>
    /// Verifies that the animation system works correctly with multiple time updates.
    /// </summary>
    [Fact]
    public void AnimationSystem_IntegrationWithTimeUpdates_WorksCorrectly()
    {
        // Arrange
        var gui = CreateTestGui();
        // gui.Time.Update();

        var animation = gui.GetAnimationFloat();
        animation.AnimateTo(10f, 0.1f, Easing.Linear);

        // Act - Simulate multiple time updates
        var values = new List<float>();
        for (var i = 0; i < 5; i++)
        {
            Thread.Sleep(30);
            // gui.Time.Update();
            values.Add(animation.GetValue());
        }

        // Assert
        Assert.True(values.Count == 5);

        // Values should generally increase (with some tolerance for timing variations)
        var finalValue = values.Last();
        Assert.True(finalValue >= 0f && finalValue <= 10f);

        // The animation should eventually reach the target
        if (finalValue < 10f)
        {
            Assert.True(animation.IsAnimating);
        }
        else
        {
            Assert.False(animation.IsAnimating);
        }
    }

    /// <summary>
    /// Verifies that the Gui returns zero counts when no animations have been created.
    /// </summary>
    [Fact]
    public void AnimationSystem_NoAnimationManager_ReturnsZeroCounts()
    {
        // Arrange
        var gui = CreateTestGui();
        // Don't call any animation methods to keep manager uninitialized

        // Act & Assert
        Assert.Equal(0, gui.ActiveAnimationCount);
        Assert.Equal(0, gui.RunningAnimationCount);

        // Clear should not throw
        gui.ClearAnimations();
    }
}
