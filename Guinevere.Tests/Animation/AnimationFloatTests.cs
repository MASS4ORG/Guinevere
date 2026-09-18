namespace Guinevere.Tests.Animation;

/// <summary>
/// Tests for the <see cref="AnimationFloat"/> class.
/// </summary>
public class AnimationFloatTests
{
    Time CreateTestTime() => new();

    /// <summary>
    /// Verifies that the constructor sets the initial value correctly.
    /// </summary>
    [Fact]
    public void Constructor_SetsInitialValue()
    {
        // Arrange
        var time = CreateTestTime();
        var initialValue = 5.0f;

        // Act
        var animationFloat = new AnimationFloat(initialValue, time);

        // Assert
        Assert.Equal(initialValue, animationFloat.GetValue());
        Assert.Equal(initialValue, animationFloat.TargetValue);
        Assert.False(animationFloat.IsAnimating);
    }

    /// <summary>
    /// Verifies that <see cref="AnimationFloat.SetValue"/> updates both current and target values.
    /// </summary>
    [Fact]
    public void SetValue_UpdatesCurrentAndTargetValue()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(0f, time);
        var newValue = 10.0f;

        // Act
        animationFloat.SetValue(newValue);

        // Assert
        Assert.Equal(newValue, animationFloat.GetValue());
        Assert.Equal(newValue, animationFloat.TargetValue);
        Assert.False(animationFloat.IsAnimating);
    }

    /// <summary>
    /// Verifies that <see cref="AnimationFloat.AnimateTo"/> with zero duration sets the value immediately.
    /// </summary>
    [Fact]
    public void AnimateTo_WithZeroDuration_SetsValueImmediately()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(0f, time);
        var targetValue = 10.0f;

        // Act
        animationFloat.AnimateTo(targetValue, 0f, Easing.Linear);

        // Assert
        Assert.Equal(targetValue, animationFloat.GetValue());
        Assert.Equal(targetValue, animationFloat.TargetValue);
        Assert.False(animationFloat.IsAnimating);
    }

    /// <summary>
    /// Verifies that <see cref="AnimationFloat.AnimateTo"/> with negative duration sets the value immediately.
    /// </summary>
    [Fact]
    public void AnimateTo_WithNegativeDuration_SetsValueImmediately()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(0f, time);
        var targetValue = 10.0f;

        // Act
        animationFloat.AnimateTo(targetValue, -1f, Easing.Linear);

        // Assert
        Assert.Equal(targetValue, animationFloat.GetValue());
        Assert.Equal(targetValue, animationFloat.TargetValue);
        Assert.False(animationFloat.IsAnimating);
    }

    /// <summary>
    /// Verifies that <see cref="AnimationFloat.AnimateTo"/> starts the animation.
    /// </summary>
    [Fact]
    public void AnimateTo_StartsAnimation()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(0f, time);
        var targetValue = 10.0f;
        var duration = 1.0f;

        // Act
        animationFloat.AnimateTo(targetValue, duration, Easing.Linear);

        // Assert
        Assert.Equal(targetValue, animationFloat.TargetValue);
        Assert.True(animationFloat.IsAnimating);
    }

    /// <summary>
    /// Verifies that <see cref="AnimationFloat.GetValue"/> updates the animation progress over time.
    /// </summary>
    [Fact]
    public void GetValue_UpdatesAnimationProgress()
    {
        // Arrange
        var time = CreateTestTime();
        time.Update(.1);
        var animationFloat = new AnimationFloat(0f, time);
        var targetValue = 10.0f;
        var duration = 1.0f;

        // Act
        animationFloat.AnimateTo(targetValue, duration, Easing.Linear);

        // Simulate time progression
        Thread.Sleep(500); // 0.5 seconds
        time.Update(.1);
        var midValue = animationFloat.GetValue();

        // Assert
        Assert.True(midValue > 0f && midValue < targetValue, $"Expected value between 0 and {targetValue}, got {midValue}");
        Assert.True(animationFloat.IsAnimating);
    }

    /// <summary>
    /// Verifies that the animation completes after the specified duration.
    /// </summary>
    [Fact]
    public void GetValue_CompletesAnimationAfterDuration()
    {
        // Arrange
        var time = CreateTestTime();
        time.Update(1); // Initialize time
        var animationFloat = new AnimationFloat(0f, time);
        var targetValue = 10.0f;
        var duration = 0.1f; // Short duration for testing

        // Act
        animationFloat.AnimateTo(targetValue, duration, Easing.Linear);

        // Simulate time progression beyond duration
        Thread.Sleep(200); // 0.2 seconds (more than duration)
        time.Update(1);
        var finalValue = animationFloat.GetValue();

        // Assert
        Assert.Equal(targetValue, finalValue);
        Assert.False(animationFloat.IsAnimating);
    }

    /// <summary>
    /// Verifies that the implicit operator returns the current value.
    /// </summary>
    [Fact]
    public void ImplicitOperator_ReturnsCurrentValue()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(5.0f, time);

        // Act
        float value = animationFloat;

        // Assert
        Assert.Equal(5.0f, value);
    }

    /// <summary>
    /// Verifies that the greater-than operator compares the current value.
    /// </summary>
    [Fact]
    public void GreaterThanOperator_ComparesCurrentValue()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(5.0f, time);

        // Act & Assert
        Assert.True(animationFloat > 3.0f);
        Assert.False(animationFloat > 7.0f);
    }

    /// <summary>
    /// Verifies that the less-than operator compares the current value.
    /// </summary>
    [Fact]
    public void LessThanOperator_ComparesCurrentValue()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(5.0f, time);

        // Act & Assert
        Assert.True(animationFloat < 7.0f);
        Assert.False(animationFloat < 3.0f);
    }

    /// <summary>
    /// Verifies that the greater-than-or-equal operator compares the current value.
    /// </summary>
    [Fact]
    public void GreaterThanOrEqualOperator_ComparesCurrentValue()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(5.0f, time);

        // Act & Assert
        Assert.True(animationFloat >= 5.0f);
        Assert.True(animationFloat >= 3.0f);
        Assert.False(animationFloat >= 7.0f);
    }

    /// <summary>
    /// Verifies that the less-than-or-equal operator compares the current value.
    /// </summary>
    [Fact]
    public void LessThanOrEqualOperator_ComparesCurrentValue()
    {
        // Arrange
        var time = CreateTestTime();
        var animationFloat = new AnimationFloat(5.0f, time);

        // Act & Assert
        Assert.True(animationFloat <= 5.0f);
        Assert.True(animationFloat <= 7.0f);
        Assert.False(animationFloat <= 3.0f);
    }

    /// <summary>
    /// Verifies that different easing functions produce expected intermediate values.
    /// </summary>
    [Fact]
    public void AnimateTo_WithDifferentEasingFunctions_ProducesExpectedResults()
    {
        // Arrange
        var time = CreateTestTime();
        time.Update(.01);
        var animationFloat = new AnimationFloat(0f, time);
        var targetValue = 10.0f;
        var duration = 0.1f;

        // Test Linear easing
        animationFloat.AnimateTo(targetValue, duration, Easing.Linear);
        // Thread.Sleep(50); // Half-duration
        time.Update(.050);
        var linearMidValue = animationFloat.GetValue();

        // Reset and test EaseIn
        animationFloat.SetValue(0f);
        animationFloat.AnimateTo(targetValue, duration, Easing.EaseIn);
        // Thread.Sleep(50); // Half-duration
        time.Update(.050);
        var easeInMidValue = animationFloat.GetValue();

        // Assert
        Assert.True(linearMidValue > easeInMidValue,
            $"Linear should progress faster than EaseIn at midpoint. Linear: {linearMidValue}, EaseIn: {easeInMidValue}");
    }

    /// <summary>
    /// Verifies that chained animations work correctly in sequence.
    /// </summary>
    [Fact]
    public void AnimateTo_ChainedAnimations_WorksCorrectly()
    {
        // Arrange
        var time = CreateTestTime();
        time.Update(1);
        var animationFloat = new AnimationFloat(0f, time);
        var firstTarget = 5.0f;
        var secondTarget = 10.0f;
        var duration = 0.1f;

        // Act
        animationFloat.AnimateTo(firstTarget, duration, Easing.Linear);
        // Thread.Sleep(120); // Wait for the first animation to complete
        time.Update(120);
        var firstValue = animationFloat.GetValue();

        animationFloat.AnimateTo(secondTarget, duration, Easing.Linear);
        // Thread.Sleep(50); // Halfway through the second animation
        time.Update(.050);
        var secondValue = animationFloat.GetValue();

        // Assert
        Assert.Equal(firstTarget, firstValue, 1); // Allow small tolerance
        Assert.True(secondValue > firstTarget && secondValue < secondTarget,
            $"Second animation should be in progress. Value: {secondValue}, expected between {firstTarget} and {secondTarget}");
    }
}
