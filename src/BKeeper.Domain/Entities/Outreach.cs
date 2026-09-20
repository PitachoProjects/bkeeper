using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Outreach : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public Guid? AlertId { get; set; }
    public NotificationChannel Channel { get; set; }
    public string? TemplateKey { get; set; }
    public string Body { get; set; } = string.Empty;
    public OutreachSentBy SentBy { get; set; }
    public Guid? SentByUserId { get; set; }
    public OutreachStatus Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public bool IsHoldout { get; set; }

    public Member? Member { get; set; }
}
