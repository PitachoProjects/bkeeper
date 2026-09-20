using BKeeper.Domain.Enums;

namespace BKeeper.Application.Metrics;

/// <summary>Flattened booking fact used to build <see cref="Domain.Rules.MemberMetrics"/> for one member.</summary>
public record BookingFact(DateOnly SessionDate, BookingStatus Status, DateTimeOffset? BookedAt);
