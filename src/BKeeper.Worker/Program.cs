using BKeeper.Infrastructure;
using BKeeper.Infrastructure.Pipeline;
using Hangfire;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBKeeperInfrastructure(builder.Configuration);

var host = builder.Build();

// Migrations are applied by BKeeper.Api on startup (compose waits on its healthcheck before
// starting this worker) — the worker itself never runs migrations, avoiding a two-writer race.

// D10: daily rule run at 05:30. ponytail: one fixed cron for all boxes rather than per-box
// timezone-aware scheduling — every box is Europe/Lisbon by default for now anyway.
var recurringJobs = host.Services.GetRequiredService<IRecurringJobManager>();
recurringJobs.AddOrUpdate<DailyRulePipeline>(
    "daily-rule-pipeline",
    pipeline => pipeline.RunForAllBoxesAsync(DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None),
    "30 5 * * *");

// §6.4: escalation + claimed-idle release + auto-expire + auto-resolve-on-return, every 15 minutes.
recurringJobs.AddOrUpdate<EscalationJob>(
    "escalation-job",
    job => job.RunForAllBoxesAsync(CancellationToken.None),
    "*/15 * * * *");

host.Run();
