using BKeeper.Domain.Common;
using BKeeper.Domain.Enums;

namespace BKeeper.Domain.Entities;

public class Workout : BoxScopedEntity
{
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Source { get; set; } = "import";

    public List<WorkoutTag> Tags { get; set; } = new();
}

public class WorkoutTag : BoxScopedEntity
{
    public Guid WorkoutId { get; set; }
    public WorkoutTagType Tag { get; set; }
    public double Weight { get; set; }
    public WorkoutTagSource Source { get; set; } = WorkoutTagSource.Rule;
    public double Confidence { get; set; }

    public Workout? Workout { get; set; }
}
