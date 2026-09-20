namespace BKeeper.Domain.Entities;

/// <summary>A CrossFit box (tenant). Every other table is scoped to a Box via BoxId.</summary>
public class Box
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "Europe/Lisbon";
    public string DefaultLanguage { get; set; } = "pt-PT";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
