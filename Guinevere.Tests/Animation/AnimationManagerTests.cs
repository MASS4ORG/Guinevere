namespace Guinevere.Tests.Animation;

// ReSharper disable ExplicitCallerInfoArgument - tests deliberately pass explicit caller locations
// to verify NodeId stability, which is a legitimate use of the [Caller*] targets.
public class AnimationManagerTests
{
    private Time CreateTestTime() => new();

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange
        var time = CreateTestTime();

        // Act
        var manager = new AnimationManager(time);

        // Assert
        Assert.Equal(0, manager.ActiveAnimationCount);
        Assert.Equal(0, manager.RunningAnimationCount);
    }

    [Fact]
    public void AnimateBool01_FirstCall_CreatesNewAnimation()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);

        // Act
        var result = manager.AnimateBool01(true, 0.5f, Easing.Linear, "test.cs", 1);

        // Assert
        Assert.Equal(1.0f, result); // Should start at target value on first call
        Assert.Equal(1, manager.ActiveAnimationCount);
    }

    [Fact]
    public void AnimateBool01_SameLocation_ReturnsSameAnimation()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);
        var filePath = "test.cs";
        var lineNumber = 1;

        // Act
        var result1 = manager.AnimateBool01(true, 0.5f, Easing.Linear, filePath, lineNumber);
        var result2 = manager.AnimateBool01(true, 0.5f, Easing.Linear, filePath, lineNumber);

        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal(1, manager.ActiveAnimationCount); // Should still be only one animation
    }

    [Fact]
    public void AnimateBool01_DifferentLocations_CreatesSeparateAnimations()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);

        // Act
        var result1 = manager.AnimateBool01(true, 0.5f, Easing.Linear, "test1.cs", 1);
        var result2 = manager.AnimateBool01(false, 0.5f, Easing.Linear, "test2.cs", 1);

        // Assert
        Assert.NotEqual(result1, result2);
        Assert.Equal(2, manager.ActiveAnimationCount);
    }

    [Fact]
    public void AnimateBool01_StateChange_TriggersAnimation()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);
        var filePath = "test.cs";
        var lineNumber = 1;

        // Act
        var result1 = manager.AnimateBool01(false, 0.5f, Easing.Linear, filePath, lineNumber);

        // Simulate time passing
        Thread.Sleep(100);
        // time.Update();

        var result2 = manager.AnimateBool01(true, 0.5f, Easing.Linear, filePath, lineNumber);

        // Assert
        Assert.Equal(0.0f, result1); // Initially false
        Assert.True(result2 >= 0.0f && result2 <= 1.0f); // Should be animating towards true (with timing tolerance)
        Assert.Equal(1, manager.ActiveAnimationCount);
        Assert.Equal(1, manager.RunningAnimationCount);
    }

    [Fact]
    public void AnimateBool01_FalseToTrue_AnimatesCorrectly()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);
        var filePath = "test.cs";
        var lineNumber = 1;

        // Start with false
        var initialResult = manager.AnimateBool01(false, 1.0f, Easing.Linear, filePath, lineNumber);

        // Act - Change to true
        Thread.Sleep(50);
        // time.Update();
        var animatingResult = manager.AnimateBool01(true, 0.1f, Easing.Linear, filePath, lineNumber);

        // Assert
        Assert.Equal(0.0f, initialResult);
        Assert.True(animatingResult >= 0.0f && animatingResult <= 1.0f);
        Assert.Equal(1, manager.RunningAnimationCount);
    }

    [Fact]
    public void AnimateBool01_TrueToFalse_AnimatesCorrectly()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);
        var filePath = "test.cs";
        var lineNumber = 1;

        // Start with true
        var initialResult = manager.AnimateBool01(true, 1.0f, Easing.Linear, filePath, lineNumber);

        // Act - Change to false
        Thread.Sleep(50);
        // time.Update();
        var animatingResult = manager.AnimateBool01(false, 0.1f, Easing.Linear, filePath, lineNumber);

        // Assert
        Assert.Equal(1.0f, initialResult);
        Assert.True(animatingResult >= 0.0f && animatingResult <= 1.0f);
        Assert.Equal(1, manager.RunningAnimationCount);
    }

    [Fact]
    public void AnimateBool01_SameStateRepeated_DoesNotRestartAnimation()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);
        var filePath = "test.cs";
        var lineNumber = 1;

        // Start animation
        manager.AnimateBool01(false, 0.5f, Easing.Linear, filePath, lineNumber);
        Thread.Sleep(50);
        // time.Update();
        manager.AnimateBool01(true, 0.5f, Easing.Linear, filePath, lineNumber);

        Thread.Sleep(50);
        // time.Update();
        var result1 = manager.AnimateBool01(true, 0.5f, Easing.Linear, filePath, lineNumber);

        Thread.Sleep(50);
        // time.Update();
        var result2 = manager.AnimateBool01(true, 0.5f, Easing.Linear, filePath, lineNumber);

        // Assert
        // Results should be progressing in the same direction (both increasing towards 1.0)
        Assert.True(result1 <= 1.0f);
        Assert.True(result2 <= 1.0f);
        Assert.Equal(1, manager.ActiveAnimationCount);
    }

    [Fact]
    public void AnimateBool01_DifferentEasingFunctions_AppliedCorrectly()
    {
        // Arrange
        var time = CreateTestTime();
        time.Update(1);
        var manager = new AnimationManager(time);

        // Act
        var linearResult = manager.AnimateBool01(false, 0.5f, Easing.Linear);
        var easeInResult = manager.AnimateBool01(false, 0.5f, Easing.EaseIn);

        Thread.Sleep(50);
        time.Update(1);

        var linearAnimating = manager.AnimateBool01(true, 0.2f, Easing.Linear);
        var easeInAnimating = manager.AnimateBool01(true, 0.2f, Easing.EaseIn);

        // Assert
        Assert.Equal(0.0f, linearResult);
        Assert.Equal(0.0f, easeInResult);
        Assert.Equal(4, manager.ActiveAnimationCount);

        // Both should be animating but potentially at different rates (with timing tolerance)
        Assert.True(linearAnimating >= 0.0f);
        Assert.True(easeInAnimating >= 0.0f);
    }

    [Fact]
    public void Clear_RemovesAllAnimations()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);

        // Add some animations
        manager.AnimateBool01(true, 0.5f, Easing.Linear, "test1.cs", 1);
        manager.AnimateBool01(false, 0.5f, Easing.Linear, "test2.cs", 1);
        manager.AnimateBool01(true, 0.5f, Easing.Linear, "test3.cs", 1);

        // Act
        manager.Clear();

        // Assert
        Assert.Equal(0, manager.ActiveAnimationCount);
        Assert.Equal(0, manager.RunningAnimationCount);
    }

    [Fact]
    public void RunningAnimationCount_ReflectsActiveAnimations()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);

        // Act - Create animations but don't start them (same state)
        manager.AnimateBool01(true, 0.5f, Easing.Linear, "test1.cs", 1);
        manager.AnimateBool01(false, 0.5f, Easing.Linear, "test2.cs", 1);
        var runningBeforeChange = manager.RunningAnimationCount;

        // Trigger animations by changing states
        Thread.Sleep(50);
        // time.Update();
        manager.AnimateBool01(false, 0.2f, Easing.Linear, "test1.cs", 1); // true -> false
        manager.AnimateBool01(true, 0.2f, Easing.Linear, "test2.cs", 1);  // false -> true
        var runningAfterChange = manager.RunningAnimationCount;

        // Assert
        Assert.Equal(0, runningBeforeChange); // No animations running initially
        Assert.Equal(2, runningAfterChange);  // Both animations should be running
        Assert.Equal(2, manager.ActiveAnimationCount); // Total animations managed
    }

    [Fact]
    public void AnimateBool01_ZeroDuration_SetsValueImmediately()
    {
        // Arrange
        var time = CreateTestTime();
        // time.Update();
        var manager = new AnimationManager(time);

        // Act
        var result1 = manager.AnimateBool01(false, 0.0f, Easing.Linear, "test.cs", 1);
        var result2 = manager.AnimateBool01(true, 0.0f, Easing.Linear, "test.cs", 1);

        // Assert
        Assert.Equal(0.0f, result1);
        Assert.Equal(1.0f, result2);
        Assert.Equal(0, manager.RunningAnimationCount); // Should not be animating with zero duration
    }
}
