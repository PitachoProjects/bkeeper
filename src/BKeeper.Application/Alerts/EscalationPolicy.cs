using BKeeper.Domain.Enums;

namespace BKeeper.Application.Alerts;

public enum EscalationAction { None, EscalateToManager, EscalateToOwner, ReleaseIdleClaim, AutoExpire }

public record AlertSnapshot(AlertSeverity Severity, AlertStatus Status, UserRole AssignedRole, DateTimeOffset CreatedAt, DateTimeOffset LastActivityAt);

/// <summary>
/// Pure SLA/escalation rules from plan §6.4. All alerts start assigned to Coach (a single-assignee
/// model — see docs/DECISIONS.md on why red's "Coach + Manager" dual visibility collapses to "starts
/// at Coach, escalates to Manager fast" here) and escalate up the ladder while unclaimed.
/// </summary>
public static class EscalationPolicy
{
    public static readonly TimeSpan RedEscalation1 = TimeSpan.FromHours(24);
    public static readonly TimeSpan RedEscalation2 = TimeSpan.FromHours(48);
    public static readonly TimeSpan RedIdleRelease = TimeSpan.FromHours(48);

    public static readonly TimeSpan AmberEscalation1 = TimeSpan.FromDays(3);
    public static readonly TimeSpan AmberEscalation2 = TimeSpan.FromDays(7);
    public static readonly TimeSpan AmberIdleRelease = TimeSpan.FromDays(5);

    public static readonly TimeSpan InfoAutoExpire = TimeSpan.FromDays(14);

    public static EscalationAction Evaluate(AlertSnapshot alert, DateTimeOffset now)
    {
        if (alert.Status is AlertStatus.Claimed or AlertStatus.InProgress)
        {
            var idleThreshold = alert.Severity == AlertSeverity.Red ? RedIdleRelease : AmberIdleRelease;
            if (alert.Severity != AlertSeverity.Info && now - alert.LastActivityAt >= idleThreshold)
                return EscalationAction.ReleaseIdleClaim;
            return EscalationAction.None;
        }

        if (alert.Status is not (AlertStatus.New or AlertStatus.Escalated)) return EscalationAction.None;

        return alert.Severity switch
        {
            AlertSeverity.Red when now - alert.CreatedAt >= RedEscalation2 && alert.AssignedRole != UserRole.Owner => EscalationAction.EscalateToOwner,
            AlertSeverity.Red when now - alert.CreatedAt >= RedEscalation1 && alert.AssignedRole == UserRole.Coach => EscalationAction.EscalateToManager,
            AlertSeverity.Amber when now - alert.CreatedAt >= AmberEscalation2 && alert.AssignedRole != UserRole.Owner => EscalationAction.EscalateToOwner,
            AlertSeverity.Amber when now - alert.CreatedAt >= AmberEscalation1 && alert.AssignedRole == UserRole.Coach => EscalationAction.EscalateToManager,
            AlertSeverity.Info when now - alert.CreatedAt >= InfoAutoExpire => EscalationAction.AutoExpire,
            _ => EscalationAction.None,
        };
    }
}
