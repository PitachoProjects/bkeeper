namespace BKeeper.Application.Ml;

public record MlBookingFact(DateOnly SessionDate, string Status, DateOnly? BookedAt, string? Window, IReadOnlyDictionary<string, double> TypeWeights);
public record MlMemberScoreRequest(Guid MemberId, DateOnly JoinDate, double? PlanFreqPerWeek, DateOnly AsOf, IReadOnlyList<MlBookingFact> Facts);
public record MlMemberScoreResult(Guid MemberId, double PChurn28d, string Band, IReadOnlyList<string> TopReasons, string ModelVersion, string ModelType);

/// <summary>Model type identifiers, matching the Python service's <c>model_registry.DEFAULT_MODEL_TYPE</c>
/// (ml/app/model_registry.py) and <c>logistic.MODEL_TYPE</c> (ml/app/logistic.py) exactly.</summary>
public static class MlModelTypes
{
    public const string LightGbmEnsemble = "lightgbm_ensemble";
    public const string LogisticRegression = "logistic_regression";
}

/// <summary>Port to the Python scoring service (plan §8). Ships raw booking facts — the service owns
/// all feature computation, so there is exactly one feature implementation (see ml/app/features.py's
/// doc comment for why that sidesteps the plan's cross-language "feature parity" test).
///
/// Two methods, not one parameterised call: <see cref="ScoreAsync"/> is the original Stage C
/// (LightGBM ensemble, <see cref="MlModelTypes.LightGbmEnsemble"/>) path R13 is wired to; <see
/// cref="ScoreLogisticAsync"/> is the separate Stage B (logistic regression,
/// <see cref="MlModelTypes.LogisticRegression"/>) comparison path — same request shape, a different
/// model, never consulted for alerting.</summary>
public interface IMlScoringClient
{
    Task<IReadOnlyList<MlMemberScoreResult>> ScoreAsync(IReadOnlyList<MlMemberScoreRequest> members, CancellationToken ct = default);

    Task<IReadOnlyList<MlMemberScoreResult>> ScoreLogisticAsync(IReadOnlyList<MlMemberScoreRequest> members, CancellationToken ct = default);
}
