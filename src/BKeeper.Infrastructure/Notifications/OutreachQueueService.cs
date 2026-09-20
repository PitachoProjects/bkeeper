using BKeeper.Application.Notifications;
using BKeeper.Domain.Entities;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Notifications;

/// <summary>
/// Decides whether an outreach should go out (plan §6.5) and queues it. Never sends directly —
/// <see cref="OutreachDispatcher"/> picks up Queued rows outside quiet hours. This two-phase split
/// exists because the daily rule pipeline runs at 05:30, which is itself inside quiet hours, so
/// "decide" and "actually send" can't be the same step.
/// </summary>
public class OutreachQueueService(BKeeperDbContext db, ILogger<OutreachQueueService> logger)
{
    /// <param name="templateKey">A key from <see cref="TemplateCatalog"/>, or null when <paramref name="rawBody"/> is used instead.</param>
    /// <param name="rawBody">Coach-authored free text — takes precedence over <paramref name="templateKey"/> when set (plan §10: "free text or template-with-edit").</param>
    public async Task<Outreach?> QueueAsync(
        Guid boxId, Guid memberId, Guid? alertId, OutreachSentBy sender, Guid? sentByUserId,
        AlertSeverity severity, string? templateKey, string language, IReadOnlyDictionary<string, string> variables,
        string? rawBody = null, CancellationToken ct = default)
    {
        var autoSentLast7d = sender == OutreachSentBy.System && await db.Outreaches.AnyAsync(o =>
            o.MemberId == memberId && o.SentBy == OutreachSentBy.System &&
            o.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-7) && o.Status != OutreachStatus.Failed, ct);
        var isHoldout = sender == OutreachSentBy.System && NotificationPolicy.IsHoldout(memberId);
        var localTime = TimeOnly.FromDateTime(DateTime.UtcNow);

        foreach (var channel in NotificationPolicy.ChannelFallbackOrder)
        {
            var consentDenied = await db.MemberConsents.AnyAsync(c => c.MemberId == memberId && c.Channel == channel && !c.Granted, ct);
            var action = NotificationPolicy.Decide(sender, severity, autoSentLast7d, isHoldout, hasConsent: !consentDenied, localTime);

            if (action is NotificationAction.RequiresHuman or NotificationAction.DroppedFrequencyCap)
            {
                logger.LogInformation("Outreach suppressed for member {MemberId}: {Action}", memberId, action);
                return null;
            }
            if (action == NotificationAction.NoConsent) continue;

            string body;
            if (rawBody is not null)
            {
                body = rawBody;
            }
            else if (templateKey is null || !TemplateCatalog.TryRender(templateKey, language, variables, out body))
            {
                logger.LogWarning("Unknown template key {TemplateKey}", templateKey);
                return null;
            }

            var isHoldoutLog = action == NotificationAction.LoggedHoldout;
            var outreach = new Outreach
            {
                BoxId = boxId,
                MemberId = memberId,
                AlertId = alertId,
                Channel = channel,
                TemplateKey = templateKey,
                Body = isHoldoutLog ? "[holdout — no message sent]" : body,
                SentBy = sender,
                SentByUserId = sentByUserId,
                Status = OutreachStatus.Queued,
                IsHoldout = isHoldoutLog,
            };
            db.Outreaches.Add(outreach);
            await db.SaveChangesAsync(ct);
            return outreach;
        }

        logger.LogInformation("Outreach skipped for member {MemberId}: no channel has consent", memberId);
        return null;
    }
}
