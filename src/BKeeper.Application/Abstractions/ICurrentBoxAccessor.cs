namespace BKeeper.Application.Abstractions;

/// <summary>The tenant (box) the current request/job is scoped to. Backed by an AsyncLocal in Infrastructure.</summary>
public interface ICurrentBoxAccessor
{
    Guid BoxId { get; }
    bool HasBox { get; }
}
