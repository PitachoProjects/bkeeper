using BKeeper.Application.Evaluations;
using BKeeper.Application.Notifications;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Alerts;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Notifications;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Pipeline;

/// <summary>
/// Daily job for plan §7's R10 (goal at risk) and R11 (evaluation overdue). Runs alongside
/// DailyRulePipeline but is kept separate since goals/evaluations don't fit the per-member
/// MemberMetrics+IRule shape the attendance rules use.
/// </summary>
public class GoalsEvaluationsJob(
    BKeeperDbContext db, CurrentBoxAccessor currentBox, OutreachQueueService outreachQueue,
    SimpleAlertService alertService, ILogger<GoalsEvaluationsJob> logger)
{
    public async Task RunForAllBoxesAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, asOf, ct);
            }
        }
    }

    public async Task RunForBoxAsync(Guid boxId, DateOnly asOf, CancellationToken ct = default)
    {
        var ruleConfigs = await db.RuleConfigs.ToDictionaryAsync(r => r.RuleCode, ct);
        var goalAlerts = ruleConfigs.TryGetValue("R10", out var r10) && !r10.Enabled ? 0 : await CheckGoalsAtRiskAsync(boxId, asOf, ct);
        var evalReminders = ruleConfigs.TryGetValue("R11", out var r11) && !r11.Enabled ? 0 : await CheckOverdueEvaluationsAsync(boxId, asOf, ct);
        await db.SaveChangesAsync(ct);
        if (goalAlerts > 0 || evalReminders > 0)
            logger.LogInformation("Goals/evaluations job for box {BoxId}: {GoalAlerts} goal-at-risk alerts, {Reminders} eval reminders/tasks", boxId, goalAlerts, evalReminders);
    }

    private async Task<int> CheckGoalsAtRiskAsync(Guid boxId, DateOnly asOf, CancellationToken ct)
    {
        var goals = await db.Goals.Where(g => g.Status == GoalStatus.Active).Include(g => g.Progress).ToListAsync(ct);
        var created = 0;

        foreach (var goal in goals)
        {
            var latest = goal.Progress.OrderByDescending(p => p.Date).FirstOrDefault();
            var input = new GoalRiskInput(goal.TargetDate, goal.BaselineValue, goal.TargetValue, latest?.Date, latest?.Value, goal.CreatedAt.Date.ToDateOnly());
            if (!GoalRiskEvaluator.IsAtRisk(input, asOf)) continue;

            var evidence = new Dictionary<string, object> { ["goal_id"] = goal.Id, ["description"] = goal.Description };
            var isNew = await alertService.CreateOrAppendAsync(boxId, goal.MemberId, "R10", AlertFamily.Goal, AlertSeverity.Amber, evidence, ct);
            if (isNew) created++;
        }

        return created;
    }

    private async Task<int> CheckOverdueEvaluationsAsync(Guid boxId, DateOnly asOf, CancellationToken ct)
    {
        var pendingLinks = await db.EvaluationFormLinks.Where(l => l.UsedAt == null).ToListAsync(ct);
        var acted = 0;

        foreach (var link in pendingLinks)
        {
            var age = asOf.DayNumber - DateOnly.FromDateTime(link.CreatedAt.Date).DayNumber;

            if (age >= 21)
            {
                var evidence = new Dictionary<string, object> { ["form_link_id"] = link.Id, ["days_overdue"] = age };
                var isNew = await alertService.CreateOrAppendAsync(boxId, link.MemberId, "R11", AlertFamily.Eval, AlertSeverity.Info, evidence, ct);
                if (isNew) acted++;
            }
            else if (age >= 7 && age < 8) // send the reminder exactly once, the day it crosses 7 days
            {
                var member = await db.Members.FindAsync([link.MemberId], ct);
                var box = await db.Boxes.FindAsync([boxId], ct);
                if (member is null) continue;

                var variables = new Dictionary<string, string>
                {
                    ["first_name"] = member.Name.Split(' ', 2)[0],
                    ["box_name"] = box?.Name ?? "your box",
                    ["form_link"] = $"/f/{link.Token}",
                };
                await outreachQueue.QueueAsync(boxId, member.Id, null, OutreachSentBy.System, null,
                    AlertSeverity.Info, TemplateCatalog.EvalReminder, member.Language, variables, ct: ct);
                acted++;
            }
        }

        return acted;
    }

}

file static class DateTimeOffsetExtensions
{
    public static DateOnly ToDateOnly(this DateTime dt) => DateOnly.FromDateTime(dt);
}
