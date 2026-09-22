namespace BKeeper.Application.Coaches;

public record ClassSessionCoachName(Guid BoxId, string? CoachName);
public record ExistingCoach(Guid BoxId, string Name);
public record CoachToCreate(Guid BoxId, string Name);

/// <summary>
/// Pure planning logic for promoting <see cref="Domain.Entities.ClassSession.CoachName"/> free text into
/// first-class <see cref="Domain.Entities.Coach"/> rows: one Coach per distinct (box, trimmed name),
/// skipping blanks and names a Coach already exists for. No I/O — the caller (a migration's raw-SQL
/// backfill, or the on-demand <c>POST /coaches/backfill</c> endpoint) supplies the raw facts and applies
/// the plan. Kept separate from the SQL backfill in the migration so this logic is unit-testable.
/// </summary>
public static class CoachBackfillPlanner
{
    public static IReadOnlyList<CoachToCreate> Plan(
        IReadOnlyList<ClassSessionCoachName> sessions,
        IReadOnlyList<ExistingCoach> existingCoaches)
    {
        var existingKeys = existingCoaches
            .Select(c => (c.BoxId, Name: c.Name.Trim()))
            .ToHashSet();

        return sessions
            .Select(s => (s.BoxId, Name: s.CoachName?.Trim()))
            .Where(s => !string.IsNullOrEmpty(s.Name))
            .Select(s => (s.BoxId, Name: s.Name!))
            .Distinct()
            .Where(s => !existingKeys.Contains(s))
            .Select(s => new CoachToCreate(s.BoxId, s.Name))
            .ToList();
    }
}
