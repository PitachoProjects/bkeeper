using System.Reflection;
using BKeeper.Application.Abstractions;
using BKeeper.Domain.Common;
using BKeeper.Domain.Entities;
using BKeeper.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BKeeper.Infrastructure.Persistence;

/// <summary>
/// ponytail: tenant isolation is enforced here via an EF Core global query filter on BoxId
/// (scoped through <see cref="ICurrentBoxAccessor"/>), not Postgres Row-Level-Security policies.
/// Upgrade path if a defense-in-depth requirement shows up: add `ALTER TABLE ... ENABLE ROW LEVEL
/// SECURITY` + policies in a migration, and set `app.current_box_id` per connection.
/// </summary>
public class BKeeperDbContext(DbContextOptions<BKeeperDbContext> options, ICurrentBoxAccessor currentBox)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Box> Boxes => Set<Box>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberNote> MemberNotes => Set<MemberNote>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<MembershipFreeze> MembershipFreezes => Set<MembershipFreeze>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<WorkoutTag> WorkoutTags => Set<WorkoutTag>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<MemberWeek> MemberWeeks => Set<MemberWeek>();
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<RuleConfig> RuleConfigs => Set<RuleConfig>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<ImportRowError> ImportRowErrors => Set<ImportRowError>();
    public DbSet<MemberConsent> MemberConsents => Set<MemberConsent>();
    public DbSet<Outreach> Outreaches => Set<Outreach>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalProgress> GoalProgresses => Set<GoalProgress>();
    public DbSet<EvaluationForm> EvaluationForms => Set<EvaluationForm>();
    public DbSet<EvaluationResponse> EvaluationResponses => Set<EvaluationResponse>();
    public DbSet<EvaluationFormLink> EvaluationFormLinks => Set<EvaluationFormLink>();
    public DbSet<RiskScore> RiskScores => Set<RiskScore>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Member>().HasIndex(m => new { m.BoxId, m.ExternalId });
        builder.Entity<ClassSession>().HasIndex(s => new { s.BoxId, s.ExternalId });
        builder.Entity<Booking>().HasIndex(b => new { b.BoxId, b.ExternalId });
        builder.Entity<MembershipFreeze>().HasIndex(f => new { f.BoxId, f.ExternalId });
        builder.Entity<Goal>().HasIndex(g => new { g.BoxId, g.ExternalId });
        builder.Entity<MemberWeek>().HasIndex(w => new { w.BoxId, w.MemberId, w.IsoWeekStart }).IsUnique();
        builder.Entity<Alert>().HasIndex(a => new { a.BoxId, a.Fingerprint });

        builder.Entity<Member>().HasMany(m => m.Notes).WithOne(n => n.Member).HasForeignKey(n => n.MemberId);
        builder.Entity<Member>().HasMany(m => m.Memberships).WithOne(m => m.Member).HasForeignKey(m => m.MemberId);
        builder.Entity<Membership>().HasMany(m => m.Freezes).WithOne(f => f.Membership).HasForeignKey(f => f.MembershipId);
        builder.Entity<Member>().HasMany(m => m.Bookings).WithOne(b => b.Member).HasForeignKey(b => b.MemberId);
        builder.Entity<ClassSession>().HasMany(s => s.Bookings).WithOne(b => b.Session).HasForeignKey(b => b.SessionId);
        builder.Entity<Workout>().HasMany(w => w.Tags).WithOne(t => t.Workout).HasForeignKey(t => t.WorkoutId);
        builder.Entity<Alert>().HasMany(a => a.Events).WithOne(e => e.Alert).HasForeignKey(e => e.AlertId);
        builder.Entity<ImportRun>().HasMany(r => r.RowErrors).WithOne(e => e.ImportRun).HasForeignKey(e => e.ImportRunId);
        builder.Entity<Outreach>().HasOne(o => o.Member).WithMany().HasForeignKey(o => o.MemberId);
        builder.Entity<Goal>().HasMany(g => g.Progress).WithOne(p => p.Goal).HasForeignKey(p => p.GoalId);
        builder.Entity<EvaluationForm>().HasMany(f => f.Responses).WithOne(r => r.Form).HasForeignKey(r => r.FormId);
        builder.Entity<EvaluationResponse>().HasOne(r => r.Member).WithMany().HasForeignKey(r => r.MemberId);

        builder.Entity<MemberConsent>().HasIndex(c => new { c.BoxId, c.MemberId, c.Channel }).IsUnique();
        builder.Entity<EvaluationForm>().HasIndex(f => new { f.BoxId, f.Key }).IsUnique();
        builder.Entity<EvaluationFormLink>().HasIndex(l => l.Token).IsUnique();
        builder.Entity<RiskScore>().HasIndex(r => new { r.BoxId, r.MemberId, r.SnapshotWeek }).IsUnique();
        builder.Entity<RiskScore>().Property(r => r.TopReasons).HasJsonConversion();

        builder.Entity<MemberWeek>().Property(w => w.VisitsByWindow).HasJsonConversion();
        builder.Entity<MemberWeek>().Property(w => w.VisitsByType).HasJsonConversion();
        builder.Entity<MemberProfile>().Property(p => p.UsualDays).HasJsonConversion();
        builder.Entity<MemberProfile>().Property(p => p.TypeMix).HasJsonConversion();
        builder.Entity<Alert>().Property(a => a.RuleCodes).HasJsonConversion();
        builder.Entity<Alert>().Property(a => a.Evidence).HasJsonConversion();
        builder.Entity<RuleConfig>().Property(r => r.Params).HasJsonConversion();
        builder.Entity<EvaluationForm>().Property(f => f.Schema).HasJsonConversion();
        builder.Entity<EvaluationResponse>().Property(r => r.Answers).HasJsonConversion();
        builder.Entity<EvaluationResponse>().Property(r => r.Scores).HasJsonConversion();

        ApplyBoxQueryFilters(builder);
    }

    private void ApplyBoxQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(BoxScopedEntity).IsAssignableFrom(entityType.ClrType)) continue;

            var method = typeof(BKeeperDbContext)
                .GetMethod(nameof(SetBoxFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, [builder]);
        }
    }

    private void SetBoxFilter<TEntity>(ModelBuilder builder) where TEntity : BoxScopedEntity =>
        builder.Entity<TEntity>().HasQueryFilter(e => !currentBox.HasBox || e.BoxId == currentBox.BoxId);
}
