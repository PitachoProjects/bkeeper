using BKeeper.Domain.Common;

namespace BKeeper.Domain.Entities;

/// <summary>Derived, idempotently-rebuilt weekly aggregate for one member. iso_week_start = Monday.</summary>
public class MemberWeek : BoxScopedEntity
{
    public Guid MemberId { get; set; }
    public DateOnly IsoWeekStart { get; set; }
    public int Visits { get; set; }
    public int Bookings { get; set; }
    public int NoShows { get; set; }
    public int LateCancels { get; set; }
    public Dictionary<string, int> VisitsByWindow { get; set; } = new();
    public Dictionary<string, double> VisitsByType { get; set; } = new();
    public DateOnly? FirstVisitDate { get; set; }
    public DateOnly? LastVisitDate { get; set; }
}
