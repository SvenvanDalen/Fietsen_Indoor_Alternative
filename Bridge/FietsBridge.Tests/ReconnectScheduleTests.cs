using FietsBridge.Core;
using Xunit;

namespace FietsBridge.Tests;

public class ReconnectScheduleTests
{
    [Fact]
    public void FirstAttemptQuickly_ThenEveryTwoSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(0.5), ReconnectSchedule.Delay(0, TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromSeconds(2), ReconnectSchedule.Delay(1, TimeSpan.FromSeconds(0.5)));
        Assert.Equal(TimeSpan.FromSeconds(2), ReconnectSchedule.Delay(5, TimeSpan.FromSeconds(9)));
    }

    [Fact]
    public void WithinTenSecondsThereAreSeveralAttempts()
    {
        // Pogingen beginnen op 0,5 / 2,5 / 4,5 / 6,5 / 8,5 s: een apparaat dat binnen 8 s terug is, is binnen 10 s verbonden
        // (zolang één poging zelf niet langer dan 1,5 s duurt).
        int attempts = 0;
        var elapsed = TimeSpan.Zero;
        while (ReconnectSchedule.Delay(attempts, elapsed) is { } delay && elapsed + delay <= TimeSpan.FromSeconds(10))
        {
            elapsed += delay;
            attempts++;
        }
        Assert.Equal(5, attempts);
    }

    [Fact]
    public void GivesUpAfterAMinute()
    {
        Assert.Null(ReconnectSchedule.Delay(30, TimeSpan.FromSeconds(60)));
        Assert.NotNull(ReconnectSchedule.Delay(29, TimeSpan.FromSeconds(59.9)));
    }

    [Fact]
    public void ReconnectingState_Serializes()
    {
        Assert.Equal(
            """{"type":"connection","id":"AA:BB:CC:DD:EE:FF","name":"Smart Drive","state":"reconnecting","message":""}""",
            Protocol.Connection("AA:BB:CC:DD:EE:FF", "Smart Drive", ConnectionState.Reconnecting));
    }
}
