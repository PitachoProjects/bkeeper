using BKeeper.Application.Notifications;
using BKeeper.Domain.Enums;
using Xunit;

namespace BKeeper.Tests.Unit.Notifications;

public class NotificationPolicyTests
{
    private static readonly TimeOnly Noon = new(12, 0);

    [Fact]
    public void System_RedSeverity_NeverAutoSends()
    {
        var action = NotificationPolicy.Decide(OutreachSentBy.System, AlertSeverity.Red, autoSentLast7d: false, isHoldout: false, hasConsent: true, Noon);
        Assert.Equal(NotificationAction.RequiresHuman, action);
    }

    [Fact]
    public void System_RedSeverity_IgnoresConsentAndTime()
    {
        // Red always requires a human, regardless of consent or quiet hours — checked before those gates.
        var action = NotificationPolicy.Decide(OutreachSentBy.System, AlertSeverity.Red, autoSentLast7d: false, isHoldout: false, hasConsent: false, new TimeOnly(2, 0));
        Assert.Equal(NotificationAction.RequiresHuman, action);
    }

    [Fact]
    public void System_AmberAlreadySentInLast7Days_Drops()
    {
        var action = NotificationPolicy.Decide(OutreachSentBy.System, AlertSeverity.Amber, autoSentLast7d: true, isHoldout: false, hasConsent: true, Noon);
        Assert.Equal(NotificationAction.DroppedFrequencyCap, action);
    }

    [Fact]
    public void System_Holdout_LogsWithoutSending()
    {
        var action = NotificationPolicy.Decide(OutreachSentBy.System, AlertSeverity.Amber, autoSentLast7d: false, isHoldout: true, hasConsent: true, Noon);
        Assert.Equal(NotificationAction.LoggedHoldout, action);
    }

    [Fact]
    public void NoConsent_ReturnsNoConsent_ForBothSystemAndUser()
    {
        Assert.Equal(NotificationAction.NoConsent,
            NotificationPolicy.Decide(OutreachSentBy.System, AlertSeverity.Amber, false, false, hasConsent: false, Noon));
        Assert.Equal(NotificationAction.NoConsent,
            NotificationPolicy.Decide(OutreachSentBy.User, AlertSeverity.Amber, false, false, hasConsent: false, Noon));
    }

    [Fact]
    public void User_SkipsFrequencyCapAndHoldout()
    {
        // A coach sending manually ignores the system-only gates (frequency cap, holdout) even when they'd apply.
        var action = NotificationPolicy.Decide(OutreachSentBy.User, AlertSeverity.Amber, autoSentLast7d: true, isHoldout: true, hasConsent: true, Noon);
        Assert.Equal(NotificationAction.SendNow, action);
    }

    [Fact]
    public void User_RedSeverity_StillSendsImmediately()
    {
        // The red gate is System-only; a coach can message a red-flagged member any time.
        var action = NotificationPolicy.Decide(OutreachSentBy.User, AlertSeverity.Red, false, false, hasConsent: true, Noon);
        Assert.Equal(NotificationAction.SendNow, action);
    }

    [Theory]
    [InlineData(22, 0, true)]   // 22:00 -> within quiet hours
    [InlineData(6, 0, true)]    // 06:00 -> within quiet hours (wraps past midnight)
    [InlineData(21, 30, true)]  // boundary: quiet hours start, inclusive
    [InlineData(9, 0, false)]   // boundary: quiet hours end, exclusive
    [InlineData(12, 0, false)]  // midday -> not quiet hours
    public void QuietHours_WrapsPastMidnightCorrectly(int hour, int minute, bool expectedQuiet)
    {
        var time = new TimeOnly(hour, minute);
        var action = NotificationPolicy.Decide(OutreachSentBy.User, AlertSeverity.Info, false, false, hasConsent: true, time);
        Assert.Equal(expectedQuiet ? NotificationAction.ScheduleAt9am : NotificationAction.SendNow, action);
    }

    [Fact]
    public void IsHoldout_IsDeterministic_ForTheSameMember()
    {
        var id = Guid.NewGuid();
        Assert.Equal(NotificationPolicy.IsHoldout(id), NotificationPolicy.IsHoldout(id));
    }
}
