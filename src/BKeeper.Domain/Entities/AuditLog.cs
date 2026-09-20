using BKeeper.Domain.Common;

namespace BKeeper.Domain.Entities;

/// <summary>Plan §11/§14: "exports and bulk actions are audit-logged." Never put PII in the message itself.</summary>
public class AuditLog : BoxScopedEntity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}
