namespace BKeeper.Application.Connectors;

public record ConnectorMember(string ExternalId, string Name, string? Email, string? Phone, DateOnly JoinDate, string Status);
public record ConnectorSession(string ExternalId, DateTimeOffset StartsAt, string ClassType, string? Coach, int? Capacity, string? WorkoutTitle, string? WorkoutDescription);
public record ConnectorBooking(string ExternalId, string MemberExternalId, string SessionExternalId, string Status, DateTimeOffset? BookedAt);

public record ConnectorPullResult(
    IReadOnlyList<ConnectorMember> Members,
    IReadOnlyList<ConnectorSession> Sessions,
    IReadOnlyList<ConnectorBooking> Bookings,
    string? NextCursor);

/// <summary>
/// Plan §10/Week10: the pull-since-cursor + webhook contract every real platform connector
/// implements. Excel (§4, <c>IExcelImportService</c>) stays a separate, simpler ingestion path
/// rather than being forced through this interface — a full-file import doesn't have a natural
/// "cursor" and there is no second connector yet to prove this abstraction against (see
/// docs/DECISIONS.md and docs/OPEN_QUESTIONS.md: no platform was named, so nothing implements this
/// contract in this pass — implementing one means reading that platform's real API docs first,
/// never guessing endpoints).
/// </summary>
public interface IBoxDataConnector
{
    string Name { get; }

    /// <summary>Pulls everything new/changed since <paramref name="cursor"/> (null = full pull).</summary>
    Task<ConnectorPullResult> PullSinceAsync(string? cursor, CancellationToken ct = default);

    /// <summary>Handles a provider webhook payload (delivery status, cancellations, etc.) — shape is provider-specific.</summary>
    Task HandleWebhookAsync(string payload, CancellationToken ct = default);
}
