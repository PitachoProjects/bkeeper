using BKeeper.Application.Notifications;
using BKeeper.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Notifications;

/// <summary>
/// Default provider: logs the message and returns a fake provider id — no WhatsApp Business API /
/// SendGrid / web-push credentials exist yet (plan §10 — "decide W6"). This is what lets the whole
/// consent → policy → template → send pipeline run and be demoed/tested end-to-end today.
/// Upgrade path: implement INotificationProvider per real channel (WhatsApp Cloud API/BSP, ACS or
/// SendGrid for email, web-push for push) and swap the DI registration in
/// BKeeper.Infrastructure/DependencyInjection.cs — nothing else in the pipeline changes.
/// </summary>
public class LogNotificationProvider(ILogger<LogNotificationProvider> logger) : INotificationProvider
{
    public Task<string> SendAsync(NotificationChannel channel, string to, string body, CancellationToken ct = default)
    {
        var id = $"log-{Guid.NewGuid():N}";
        logger.LogInformation("[{Channel}] to {To}: {Body} (id={Id})", channel, to, body, id);
        return Task.FromResult(id);
    }
}
