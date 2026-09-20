namespace BKeeper.Application.Ml;

public record MlBookingFact(DateOnly SessionDate, string Status, DateOnly? BookedAt, string? Window, IReadOnlyDictionary<string, double> TypeWeights);
public record MlMemberScoreRequest(Guid MemberId, DateOnly JoinDate, double? PlanFreqPerWeek, DateOnly AsOf, IReadOnlyList<MlBookingFact> Facts);
public record MlMemberScoreResult(Guid MemberId, double PChurn28d, string Band, IReadOnlyList<string> TopReasons, string ModelVersion);

/// <summary>Port to the Python scoring service (plan §8). Ships raw booking facts — the service owns
/// all feature computation, so there is exactly one feature implementation (see ml/app/features.py's
/// doc comment for why that sidesteps the plan's cross-language "feature parity" test).</summary>
public interface IMlScoringClient
{
    Task<IReadOnlyList<MlMemberScoreResult>> ScoreAsync(IReadOnlyList<MlMemberScoreRequest> members, CancellationToken ct = default);
}
