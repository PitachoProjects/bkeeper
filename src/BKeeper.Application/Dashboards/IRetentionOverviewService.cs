namespace BKeeper.Application.Dashboards;

/// <summary>Data-fetching for the retention dashboard (plan §9), extracted out of
/// <c>DashboardsController</c> so the AI narrative layer (<c>InsightsController</c>) can reuse the
/// exact same validated computation instead of re-querying or re-deriving any metric itself.</summary>
public interface IRetentionOverviewService
{
    Task<RetentionOverviewDto> BuildAsync(CancellationToken ct = default);
}
