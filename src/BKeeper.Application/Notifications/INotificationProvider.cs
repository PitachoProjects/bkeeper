using BKeeper.Domain.Enums;

namespace BKeeper.Application.Notifications;

public interface INotificationProvider
{
    /// <summary>Sends (or simulates sending) a message. Returns a provider message id for delivery-status tracking.</summary>
    Task<string> SendAsync(NotificationChannel channel, string to, string body, CancellationToken ct = default);
}
