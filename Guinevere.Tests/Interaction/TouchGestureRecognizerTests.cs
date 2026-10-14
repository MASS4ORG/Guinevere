namespace Guinevere.Tests.Interaction;

public class TouchGestureRecognizerTests
{
    [Fact]
    public void TapLongPressSwipeAndCancellationUseTimeAndTravel()
    {
        var recognizer = new TouchGestureRecognizer();
        recognizer.Process([Contact(1, 0, TouchPhase.Began)], 0);
        var tap = Assert.Single(recognizer.Process([Contact(1, 1, TouchPhase.Ended)], 0.1));
        Assert.Equal(TouchGestureKind.Tap, tap.Kind);
        recognizer.Process([Contact(2, 0, TouchPhase.Began)], 1);
        Assert.Empty(recognizer.Process([], 1.1));
        Assert.Equal(TouchGestureKind.LongPress, Assert.Single(recognizer.Process([], 1.6)).Kind);
        Assert.Empty(recognizer.Process([], 1.7));
        Assert.Empty(recognizer.Process([Contact(2, 0, TouchPhase.Ended)], 1.8));
        recognizer.Process([Contact(3, 0, TouchPhase.Began)], 2);
        var swipe = Assert.Single(recognizer.Process([Contact(3, 50, TouchPhase.Ended)], 2.2));
        Assert.Equal(TouchGestureKind.Swipe, swipe.Kind);
        Assert.Equal(new Vector2(50, 0), swipe.Delta);
        recognizer.Process([Contact(4, 0, TouchPhase.Began)], 3);
        Assert.Empty(recognizer.Process([Contact(4, 0, TouchPhase.Canceled)], 3.1));
        Assert.Empty(recognizer.Process([Contact(99, 0, TouchPhase.Moved)], 4));
        recognizer.Process([Contact(5, 0, TouchPhase.Began)], 5);
        recognizer.Process([Contact(5, 20, TouchPhase.Moved)], 5.1);
        Assert.Empty(recognizer.Process([Contact(5, 0, TouchPhase.Ended)], 5.2));
        recognizer.Process([Contact(6, 0, TouchPhase.Began)], 6);
        recognizer.Reset();
        Assert.Empty(recognizer.Process([], 10));
    }

    [Fact]
    public void TwoContactsProduceScaleAndRotationWithoutAccidentalTaps()
    {
        var recognizer = new TouchGestureRecognizer();
        recognizer.Process([Contact(1, 0, TouchPhase.Began), Contact(2, 10, TouchPhase.Began)], 0);
        var pinch = Assert.Single(recognizer.Process([Contact(2, 20, TouchPhase.Moved)], 0.1));
        Assert.Equal(TouchGestureKind.Pinch, pinch.Kind);
        Assert.Equal(2, pinch.Scale);
        var rotation = Assert.Single(recognizer.Process([
            new TouchContact(2, new Vector2(0, 20), null, TouchPhase.Moved)], 0.2));
        Assert.Equal(TouchGestureKind.Rotate, rotation.Kind);
        Assert.Equal(MathF.PI / 2, rotation.Rotation);
        Assert.Empty(recognizer.Process([Contact(1, 0, TouchPhase.Ended), Contact(2, 20, TouchPhase.Ended)], 0.3));
    }

    [Fact]
    public void ExtraAndCoincidentContactsResetPairBaseline()
    {
        var recognizer = new TouchGestureRecognizer();
        Assert.Empty(recognizer.Process([Contact(1, 0, TouchPhase.Began), Contact(2, 0, TouchPhase.Began)], 0));
        Assert.Empty(recognizer.Process([Contact(2, 10, TouchPhase.Moved)], 0.1));
        Assert.Empty(recognizer.Process([Contact(3, 20, TouchPhase.Began)], 0.2));
        Assert.Empty(recognizer.Process([Contact(3, 20, TouchPhase.Canceled)], 0.3));
        Assert.Empty(recognizer.Process([], 2));
    }

    static TouchContact Contact(long id, float x, TouchPhase phase) => new(id, new Vector2(x, 0), 0.5f, phase);
}
