namespace BKeeper.Domain.Common;

public abstract class BoxScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BoxId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
