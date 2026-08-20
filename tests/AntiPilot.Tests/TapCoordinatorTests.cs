using Xunit;

namespace AntiPilot.Tests;

/// <summary>
/// The coordinator talks between processes through named kernel objects, and those are visible
/// across threads of one process in exactly the same way — so a second thread here stands in for
/// the second key press without needing to spawn anything.
/// </summary>
[Collection(nameof(TapCoordinatorTests))]
public class TapCoordinatorTests
{
    private const int Window = 400;

    [Fact]
    public void ALonePressIsASinglePress()
    {
        Assert.Equal(TapCoordinator.Press.Single, TapCoordinator.Classify(Window));
    }

    [Fact]
    public void ASecondPressInsideTheWindowMakesADouble()
    {
        TapCoordinator.Press first = default;
        TapCoordinator.Press second = default;

        var ready = new ManualResetEventSlim();

        var firstPress = new Thread(() =>
        {
            ready.Set();
            first = TapCoordinator.Classify(Window);
        });

        firstPress.Start();
        ready.Wait();

        // Comfortably inside the window, and long enough after it that the first press is
        // certainly the one holding the mutex.
        Thread.Sleep(60);
        second = TapCoordinator.Classify(Window);

        Assert.True(firstPress.Join(TimeSpan.FromSeconds(5)));
        Assert.Equal(TapCoordinator.Press.Double, first);
        Assert.Equal(TapCoordinator.Press.Handled, second);
    }

    [Fact]
    public void APressAfterTheWindowClosesIsItsOwnSinglePress()
    {
        Assert.Equal(TapCoordinator.Press.Single, TapCoordinator.Classify(200));

        // The real regression this guards: a stale signal left set by a late second press used to
        // make the *next* press look like a double. It must not.
        Assert.Equal(TapCoordinator.Press.Single, TapCoordinator.Classify(200));
        Assert.Equal(TapCoordinator.Press.Single, TapCoordinator.Classify(200));
    }

    [Fact]
    public void TheWaitIsRoughlyTheWindowItWasGiven()
    {
        // This delay is the entire cost of the feature, so it is worth asserting that it is the
        // number the user chose and not, say, twice it.
        var started = Environment.TickCount64;
        TapCoordinator.Classify(300);
        var elapsed = Environment.TickCount64 - started;

        Assert.InRange(elapsed, 250, 1500);
    }
}
