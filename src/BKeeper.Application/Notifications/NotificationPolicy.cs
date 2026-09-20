using BKeeper.Domain.Enums;

namespace BKeeper.Application.Notifications;

public enum NotificationAction
{
    /// <summary>Red severity: never auto-send — a human contacts the member first (plan D7).</summary>
    RequiresHuman,
    /// <summary>System already auto-sent to this member in the last 7 days (max 1 automated msg / 7 days).</summary>
    DroppedFrequencyCap,
    /// <summary>10% holdout: logged for the causal-impact comparison, no message sent (plan §13).</summary>
    LoggedHoldout,
    /// <summary>No consent for this channel/purpose — caller should try the next channel in the fallback order.</summary>
    NoConsent,
    /// <summary>Within quiet hours (21:30-09:00 box time by default) — send at 09:00 instead.</summary>
    ScheduleAt9am,
    SendNow,
}

/// <summary>Pure decision logic for plan §6.5. Coach-sent messages skip the frequency cap and holdout checks.</summary>
public static class NotificationPolicy
{
    public static readonly TimeOnly QuietHoursStart = new(21, 30);
    public static readonly TimeOnly QuietHoursEnd = new(9, 0);

    public static NotificationAction Decide(
        OutreachSentBy sender,
        AlertSeverity severity,
        bool autoSentLast7d,
        bool isHoldout,
        bool hasConsent,
        TimeOnly localTime)
    {
        if (sender == OutreachSentBy.System)
        {
            if (severity == AlertSeverity.Red) return NotificationAction.RequiresHuman;
            if (autoSentLast7d) return NotificationAction.DroppedFrequencyCap;
            if (isHoldout) return NotificationAction.LoggedHoldout;
        }

        if (!hasConsent) return NotificationAction.NoConsent;

        return IsQuietHours(localTime) ? NotificationAction.ScheduleAt9am : NotificationAction.SendNow;
    }

    public static bool IsQuietHours(TimeOnly time) =>
        QuietHoursStart < QuietHoursEnd
            ? time >= QuietHoursStart && time < QuietHoursEnd
            : time >= QuietHoursStart || time < QuietHoursEnd; // wraps past midnight

    /// <summary>Deterministic 10% holdout assignment (plan §13) — stable per member without a separate table.</summary>
    public static bool IsHoldout(Guid memberId) => Math.Abs(memberId.GetHashCode()) % 10 == 0;

    public static readonly NotificationChannel[] ChannelFallbackOrder =
    [
        NotificationChannel.WhatsApp,
        NotificationChannel.Push,
        NotificationChannel.Email,
    ];
}
