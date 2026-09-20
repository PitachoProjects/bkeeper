using BKeeper.Application.Alerts;
using BKeeper.Domain.Enums;
using BKeeper.Domain.Rules;
using Xunit;

namespace BKeeper.Tests.Unit.Alerts;

public class AlertOrchestratorTests
{
    private static readonly Dictionary<string, object> Evidence = new() { ["days"] = 15 };
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesNewAlert_WhenNoOpenAlertAndNoCooldown()
    {
        var hits = new[] { new RuleHit("R01", AlertFamily.Attendance, AlertSeverity.Amber, Evidence) };
        var decisions = AlertOrchestrator.Decide(hits, new Dictionary<AlertFamily, OpenAlertState>(), new Dictionary<AlertFamily, DateTimeOffset>(), hasHumanContactLast7d: false, Now);

        var d = Assert.Single(decisions);
        Assert.Equal(FamilyAction.CreateNew, d.Action);
        Assert.Equal(AlertSeverity.Amber, d.Severity);
    }

    [Fact]
    public void AppendsToExistingOpenAlert_AndRaisesSeverity()
    {
        var hits = new[] { new RuleHit("R03", AlertFamily.Attendance, AlertSeverity.Red, Evidence) };
        var open = new Dictionary<AlertFamily, OpenAlertState> { [AlertFamily.Attendance] = new(Guid.NewGuid(), AlertSeverity.Amber) };

        var decisions = AlertOrchestrator.Decide(hits, open, new Dictionary<AlertFamily, DateTimeOffset>(), hasHumanContactLast7d: false, Now);

        var d = Assert.Single(decisions);
        Assert.Equal(FamilyAction.AppendToExisting, d.Action);
        Assert.Equal(AlertSeverity.Red, d.Severity); // max(existing amber, new red)
    }

    [Fact]
    public void SkipsDuringCooldown_AfterRecentResolution()
    {
        var hits = new[] { new RuleHit("R01", AlertFamily.Attendance, AlertSeverity.Amber, Evidence) };
        var lastResolved = new Dictionary<AlertFamily, DateTimeOffset> { [AlertFamily.Attendance] = Now.AddDays(-5) }; // amber cooldown = 14d

        var decisions = AlertOrchestrator.Decide(hits, new Dictionary<AlertFamily, OpenAlertState>(), lastResolved, hasHumanContactLast7d: false, Now);

        Assert.Equal(FamilyAction.None, Assert.Single(decisions).Action);
    }

    [Fact]
    public void CreatesAfterCooldownExpires()
    {
        var hits = new[] { new RuleHit("R01", AlertFamily.Attendance, AlertSeverity.Amber, Evidence) };
        var lastResolved = new Dictionary<AlertFamily, DateTimeOffset> { [AlertFamily.Attendance] = Now.AddDays(-15) };

        var decisions = AlertOrchestrator.Decide(hits, new Dictionary<AlertFamily, OpenAlertState>(), lastResolved, hasHumanContactLast7d: false, Now);

        Assert.Equal(FamilyAction.CreateNew, Assert.Single(decisions).Action);
    }

    [Fact]
    public void CreatesAsInfo_WhenRecentHumanContact()
    {
        var hits = new[] { new RuleHit("R01", AlertFamily.Attendance, AlertSeverity.Amber, Evidence) };
        var decisions = AlertOrchestrator.Decide(hits, new Dictionary<AlertFamily, OpenAlertState>(), new Dictionary<AlertFamily, DateTimeOffset>(), hasHumanContactLast7d: true, Now);

        var d = Assert.Single(decisions);
        Assert.Equal(FamilyAction.CreateAsInfo, d.Action);
        Assert.Equal(AlertSeverity.Info, d.Severity);
    }

    [Fact]
    public void IsSuppressed_ForFrozenOrAwayOrUnderTenure()
    {
        var m = new MemberMetrics { MemberId = Guid.NewGuid(), AsOf = default, TenureWeeks = 1, IsOnboarding = false, IsAway = false };
        Assert.True(AlertOrchestrator.IsSuppressed(m, isFrozenOrCancelled: true));

        var away = m with { IsAway = true };
        Assert.True(AlertOrchestrator.IsSuppressed(away, isFrozenOrCancelled: false));

        var underTenure = m with { TenureWeeks = 1 };
        Assert.True(AlertOrchestrator.IsSuppressed(underTenure, isFrozenOrCancelled: false));

        var eligible = m with { TenureWeeks = 10 };
        Assert.False(AlertOrchestrator.IsSuppressed(eligible, isFrozenOrCancelled: false));
    }

    [Fact]
    public void PriorityScore_RedOutranksAmberOutranksInfo()
    {
        var red = AlertOrchestrator.PriorityScore(AlertSeverity.Red, null, 1, false, false);
        var amber = AlertOrchestrator.PriorityScore(AlertSeverity.Amber, null, 1, false, false);
        var info = AlertOrchestrator.PriorityScore(AlertSeverity.Info, null, 1, false, false);
        Assert.True(red > amber);
        Assert.True(amber > info);
    }
}
