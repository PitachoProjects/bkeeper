using BKeeper.Application.Notifications;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Notifications;

/// <summary>Sends Queued, non-holdout outreach rows once it's outside quiet hours (plan §6.5).</summary>
public class OutreachDispatcher(BKeeperDbContext db, INotificationProvider provider, CurrentBoxAccessor currentBox, ILogger<OutreachDispatcher> logger)
{
    public async Task RunForAllBoxesAsync(CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, ct);
            }
        }
    }

    public async Task<int> RunForBoxAsync(Guid boxId, CancellationToken ct = default)
    {
        if (NotificationPolicy.IsQuietHours(TimeOnly.FromDateTime(DateTime.UtcNow))) return 0;

        var pending = await db.Outreaches.Include(o => o.Member)
            .Where(o => o.Status == OutreachStatus.Queued && !o.IsHoldout)
            .ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var sent = 0;
        foreach (var outreach in pending)
        {
            var to = outreach.Channel switch
            {
                NotificationChannel.WhatsApp => outreach.Member?.PhoneE164,
                NotificationChannel.Email => outreach.Member?.Email,
                _ => outreach.MemberId.ToString(), // push: no device-token model yet, addressed by member id
            };

            if (string.IsNullOrWhiteSpace(to))
            {
                outreach.Status = OutreachStatus.Failed;
                outreach.UpdatedAt = DateTimeOffset.UtcNow;
                continue;
            }

            outreach.ProviderMessageId = await provider.SendAsync(outreach.Channel, to, outreach.Body, ct);
            outreach.Status = OutreachStatus.Sent;
            outreach.UpdatedAt = DateTimeOffset.UtcNow;
            sent++;
        }

        await db.SaveChangesAsync(ct);
        if (sent > 0) logger.LogInformation("Outreach dispatcher for box {BoxId}: sent {Count}", boxId, sent);
        return sent;
    }
}
