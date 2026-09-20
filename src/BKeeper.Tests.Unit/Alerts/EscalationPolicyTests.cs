using BKeeper.Application.Alerts;
using BKeeper.Domain.Enums;
using Xunit;

namespace BKeeper.Tests.Unit.Alerts;

public class EscalationPolicyTests
{
    private static readonly DateTimeOffset Created = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static AlertSnapshot New(AlertSeverity severity, UserRole role = UserRole.Coach, DateTimeOffset? createdAt = null) =>
        new(severity, AlertStatus.New, role, createdAt ?? Created, createdAt ?? Created);

    [Fact]
    public void Red_EscalatesToManager_After24hUnclaimed()
    {
        var before = EscalationPolicy.Evaluate(New(AlertSeverity.Red), Created.AddHours(23));
        var after = EscalationPolicy.Evaluate(New(AlertSeverity.Red), Created.AddHours(24));

        Assert.Equal(EscalationAction.None, before);
        Assert.Equal(EscalationAction.EscalateToManager, after);
    }

    [Fact]
    public void Red_EscalatesToOwner_After48hUnclaimed()
    {
        var alert = New(AlertSeverity.Red, UserRole.Manager);
        var action = EscalationPolicy.Evaluate(alert, Created.AddHours(48));
        Assert.Equal(EscalationAction.EscalateToOwner, action);
    }

    [Fact]
    public void Amber_EscalatesToManager_After72hUnclaimed()
    {
        var action = EscalationPolicy.Evaluate(New(AlertSeverity.Amber), Created.AddDays(3));
        Assert.Equal(EscalationAction.EscalateToManager, action);
    }

    [Fact]
    public void Amber_EscalatesToOwner_After7DaysUnclaimed()
    {
        var alert = New(AlertSeverity.Amber, UserRole.Manager);
        var action = EscalationPolicy.Evaluate(alert, Created.AddDays(7));
        Assert.Equal(EscalationAction.EscalateToOwner, action);
    }

    [Fact]
    public void Info_NeverEscalates_ButAutoExpiresAfter14Days()
    {
        var beforeExpiry = EscalationPolicy.Evaluate(New(AlertSeverity.Info), Created.AddDays(13));
        var afterExpiry = EscalationPolicy.Evaluate(New(AlertSeverity.Info), Created.AddDays(14));

        Assert.Equal(EscalationAction.None, beforeExpiry);
        Assert.Equal(EscalationAction.AutoExpire, afterExpiry);
    }

    [Fact]
    public void ClaimedIdle_Red_ReleasesAfter48Hours()
    {
        var claimed = new AlertSnapshot(AlertSeverity.Red, AlertStatus.Claimed, UserRole.Coach, Created, Created);
        var before = EscalationPolicy.Evaluate(claimed, Created.AddHours(47));
        var after = EscalationPolicy.Evaluate(claimed, Created.AddHours(48));

        Assert.Equal(EscalationAction.None, before);
        Assert.Equal(EscalationAction.ReleaseIdleClaim, after);
    }

    [Fact]
    public void ClaimedIdle_Amber_ReleasesAfter5Days()
    {
        var claimed = new AlertSnapshot(AlertSeverity.Amber, AlertStatus.InProgress, UserRole.Coach, Created, Created);
        var action = EscalationPolicy.Evaluate(claimed, Created.AddDays(5));
        Assert.Equal(EscalationAction.ReleaseIdleClaim, action);
    }

    [Fact]
    public void ClaimedActive_NeverReleases()
    {
        // LastActivityAt refreshed just now, even though the alert is old.
        var claimed = new AlertSnapshot(AlertSeverity.Red, AlertStatus.Claimed, UserRole.Coach, Created, Created.AddDays(10));
        var action = EscalationPolicy.Evaluate(claimed, Created.AddDays(10).AddHours(1));
        Assert.Equal(EscalationAction.None, action);
    }

    [Fact]
    public void ResolvedAlert_NeverEscalates()
    {
        var resolved = new AlertSnapshot(AlertSeverity.Red, AlertStatus.Resolved, UserRole.Coach, Created, Created);
        Assert.Equal(EscalationAction.None, EscalationPolicy.Evaluate(resolved, Created.AddYears(1)));
    }

    [Fact]
    public void AlreadyEscalatedToOwner_DoesNotEscalateAgain()
    {
        var alert = New(AlertSeverity.Red, UserRole.Owner);
        Assert.Equal(EscalationAction.None, EscalationPolicy.Evaluate(alert, Created.AddDays(30)));
    }
}
