namespace Guinevere.Tests.Platform;

public class WindowCloseGateTests
{
    [Fact]
    public void ACloseIsAllowedWhenNoHandlerIsAsked()
    {
        var gate = new WindowCloseGate();

        Assert.True(gate.MayClose());
        Assert.False(gate.Approved);
    }

    [Fact]
    public void AHandlerCanVetoAClose()
    {
        var gate = new WindowCloseGate { CloseRequested = () => false };

        Assert.False(gate.MayClose());
        Assert.False(gate.Approved);
    }

    [Fact]
    public void AHandlerCanAllowAClose()
    {
        var gate = new WindowCloseGate { CloseRequested = () => true };

        Assert.True(gate.MayClose());
    }

    /// <summary>
    /// The freeze this guards against: a windowing library raises its closing event from its own close call too, so a
    /// handler answering with a close returns whatever it likes afterwards. The grant has to survive that.
    /// </summary>
    [Fact]
    public void AHandlerThatApprovesWhileVetoingItsReturnValueStillCloses()
    {
        var gate = new WindowCloseGate();
        gate.CloseRequested = () =>
        {
            gate.Approve();
            return false;
        };

        Assert.True(gate.MayClose());
        Assert.True(gate.Approved);
    }

    [Fact]
    public void AnApprovedCloseIsNeverVetoedAndSkipsTheHandler()
    {
        var asked = 0;
        var gate = new WindowCloseGate { CloseRequested = () => { asked++; return false; } };

        gate.Approve();

        Assert.True(gate.MayClose());
        Assert.Equal(0, asked);
    }

    [Fact]
    public void AHandlerAskingAboutUnsavedWorkIsOnlyConsultedOnceUntilTheAnswerArrives()
    {
        var asked = 0;
        var gate = new WindowCloseGate { CloseRequested = () => { asked++; return false; } };

        Assert.False(gate.MayClose());
        Assert.False(gate.MayClose());
        Assert.Equal(2, asked);

        // The dialog was answered with "yes", which closes the window from inside the handler.
        gate.CloseRequested = () =>
        {
            gate.Approve();
            return true;
        };

        Assert.True(gate.MayClose());
        Assert.True(gate.Approved);
    }
}
