// Test đơn vị cho quy tắc cuộc gọi (docs/05_api_contract.md mục 9, docs/tasks/phase4_calls.md).
using Comink.Domain.Calls;
using Comink.Domain.Enums;

namespace Comink.Domain.Tests;

public class CallRulesTests
{
    [Theory]
    [InlineData(CallStatus.Completed)]
    [InlineData(CallStatus.Declined)]
    [InlineData(CallStatus.Cancelled)]
    [InlineData(CallStatus.Missed)]
    [InlineData(CallStatus.Failed)]
    public void IsTerminal_TrueForEndedStates(CallStatus status) => Assert.True(CallRules.IsTerminal(status));

    [Theory]
    [InlineData(CallStatus.Ringing)]
    [InlineData(CallStatus.InProgress)]
    public void IsTerminal_FalseWhileCallIsLive(CallStatus status) => Assert.False(CallRules.IsTerminal(status));

    [Fact]
    public void CanAnswer_OnlyWhileRinging()
    {
        Assert.True(CallRules.CanAnswer(CallStatus.Ringing));
        Assert.False(CallRules.CanAnswer(CallStatus.InProgress));
        Assert.False(CallRules.CanAnswer(CallStatus.Completed));
    }

    [Fact]
    public void CanRequestToken_FalseAfterCallEnded()
    {
        Assert.True(CallRules.CanRequestToken(CallStatus.Ringing));
        Assert.True(CallRules.CanRequestToken(CallStatus.InProgress));
        Assert.False(CallRules.CanRequestToken(CallStatus.Missed));
    }

    [Fact]
    public void ComputeDurationSeconds_ReturnsNullWithoutBothMilestones()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Null(CallRules.ComputeDurationSeconds(null, now));
        Assert.Null(CallRules.ComputeDurationSeconds(now, null));
    }

    [Fact]
    public void ComputeDurationSeconds_RoundsAndNeverNegative()
    {
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(120, CallRules.ComputeDurationSeconds(start, start.AddSeconds(120)));
        Assert.Equal(0, CallRules.ComputeDurationSeconds(start, start.AddSeconds(-30)));
    }

    [Fact]
    public void ResolveEnd_PrefersCompletedWhenSomeoneJoined()
    {
        var (status, reason) = CallRules.ResolveEnd(
            anyoneJoined: true,
            cancelledByInitiator: true,
            allDeclined: true,
            timedOut: true);

        Assert.Equal(CallStatus.Completed, status);
        Assert.Equal(CallEndReason.Completed, reason);
    }

    [Fact]
    public void ResolveEnd_NoOneJoined_UsesReasonByPriority()
    {
        // Hết giờ đổ chuông được ưu tiên hơn "bị huỷ".
        var timedOut = CallRules.ResolveEnd(anyoneJoined: false, cancelledByInitiator: true, allDeclined: false, timedOut: true);
        Assert.Equal(CallStatus.Missed, timedOut.Status);
        Assert.Equal(CallEndReason.NoAnswer, timedOut.EndReason);

        var cancelled = CallRules.ResolveEnd(anyoneJoined: false, cancelledByInitiator: true, allDeclined: false, timedOut: false);
        Assert.Equal(CallStatus.Cancelled, cancelled.Status);

        var declined = CallRules.ResolveEnd(anyoneJoined: false, cancelledByInitiator: false, allDeclined: true, timedOut: false);
        Assert.Equal(CallStatus.Declined, declined.Status);

        var failed = CallRules.ResolveEnd(anyoneJoined: false, cancelledByInitiator: false, allDeclined: false, timedOut: false, failed: true);
        Assert.Equal(CallStatus.Failed, failed.Status);
        Assert.Equal(CallEndReason.Failed, failed.EndReason);
    }

    [Fact]
    public void AreParticipantsValid_RequiresDistinctWithinRange()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        Assert.True(CallRules.AreParticipantsValid([first, second]));
        Assert.False(CallRules.AreParticipantsValid([first]));
        Assert.False(CallRules.AreParticipantsValid([first, first]));
        Assert.False(CallRules.AreParticipantsValid(
            Enumerable.Range(0, CallRules.MaxParticipants + 1).Select(_ => Guid.CreateVersion7()).ToList()));
    }
}
