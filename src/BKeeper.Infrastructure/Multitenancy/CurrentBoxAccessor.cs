using BKeeper.Application.Abstractions;

namespace BKeeper.Infrastructure.Multitenancy;

public class CurrentBoxAccessor : ICurrentBoxAccessor
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public Guid BoxId => Current.Value ?? throw new InvalidOperationException("No box scope is active.");
    public bool HasBox => Current.Value.HasValue;

    /// <summary>Scopes everything run inside the using-block (including EF queries) to one box.</summary>
    public IDisposable Use(Guid boxId)
    {
        var previous = Current.Value;
        Current.Value = boxId;
        return new Restorer(previous);
    }

    private sealed class Restorer(Guid? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
