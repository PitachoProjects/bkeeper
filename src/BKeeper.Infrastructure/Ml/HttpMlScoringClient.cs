using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BKeeper.Application.Ml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BKeeper.Infrastructure.Ml;

public class MlServiceOptions
{
    public const string SectionName = "MlService";
    public string BaseUrl { get; set; } = "http://ml:8090";
    public string ServiceToken { get; set; } = "dev-ml-token-change-me";
}

public class HttpMlScoringClient(HttpClient http, IOptions<MlServiceOptions> options, ILogger<HttpMlScoringClient> logger) : IMlScoringClient
{
    public async Task<IReadOnlyList<MlMemberScoreResult>> ScoreAsync(IReadOnlyList<MlMemberScoreRequest> members, CancellationToken ct = default)
    {
        if (members.Count == 0) return [];

        var request = new ScoreRequestDto(members.Select(m => new MemberScoreRequestDto(
            m.MemberId.ToString(), m.JoinDate, m.PlanFreqPerWeek, m.AsOf,
            m.Facts.Select(f => new BookingFactDto(f.SessionDate, f.Status, f.BookedAt, f.Window, f.TypeWeights)).ToList()
        )).ToList());

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{options.Value.BaseUrl.TrimEnd('/')}/score")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Add("X-Service-Token", options.Value.ServiceToken);

        try
        {
            var response = await http.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<ScoreResponseDto>(cancellationToken: ct);
            return body?.Results.Select(r => new MlMemberScoreResult(Guid.Parse(r.MemberId), r.PChurn28d, r.Band, r.TopReasons, r.ModelVersion)).ToList()
                ?? [];
        }
        catch (Exception ex)
        {
            // ponytail: shadow mode — a scoring outage should never break the nightly pipeline.
            // Upgrade path: alert ops if this keeps failing across runs.
            logger.LogWarning(ex, "ML scoring service call failed; skipping this run's risk scores");
            return [];
        }
    }

    private record ScoreRequestDto([property: JsonPropertyName("members")] List<MemberScoreRequestDto> Members);

    private record MemberScoreRequestDto(
        [property: JsonPropertyName("member_id")] string MemberId,
        [property: JsonPropertyName("join_date")] DateOnly JoinDate,
        [property: JsonPropertyName("plan_freq_per_week")] double? PlanFreqPerWeek,
        [property: JsonPropertyName("asof")] DateOnly Asof,
        [property: JsonPropertyName("facts")] List<BookingFactDto> Facts);

    private record BookingFactDto(
        [property: JsonPropertyName("session_date")] DateOnly SessionDate,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("booked_at")] DateOnly? BookedAt,
        [property: JsonPropertyName("window")] string? Window,
        [property: JsonPropertyName("type_weights")] IReadOnlyDictionary<string, double> TypeWeights);

    private record ScoreResponseDto([property: JsonPropertyName("results")] List<MemberScoreResultDto> Results);

    private record MemberScoreResultDto(
        [property: JsonPropertyName("member_id")] string MemberId,
        [property: JsonPropertyName("p_churn_28d")] double PChurn28d,
        [property: JsonPropertyName("band")] string Band,
        [property: JsonPropertyName("top_reasons")] List<string> TopReasons,
        [property: JsonPropertyName("model_version")] string ModelVersion);
}
