using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Alert : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public AlertFamily Family { get; set; }
    public List<string> RuleCodes { get; set; } = new();
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.New;
    public UserRole AssignedRole { get; set; }
    public Guid? ClaimedBy { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? SnoozeUntil { get; set; }
    public Dictionary<string, object> Evidence { get; set; } = new();
    public string? Outcome { get; set; }
    public string? OutcomeNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    /// <summary>Stable key used to find the "same family" open alert for a member: {MemberId}:{Family}.</summary>
    public string Fingerprint { get; set; } = string.Empty;
    public DateTimeOffset? LastActivityAt { get; set; }

    public Member? Member { get; set; }
    public List<AlertEvent> Events { get; set; } = new();
}

public class AlertEvent : BoxScopedEntity
{
    public Guid AlertId { get; set; }
    public AlertEventType Type { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? Payload { get; set; }

    public Alert? Alert { get; set; }
}
