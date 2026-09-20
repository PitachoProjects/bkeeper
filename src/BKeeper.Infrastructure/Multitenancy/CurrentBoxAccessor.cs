using BKeeper.Application.Abstractions;

namespace BKeeper.Infrastructure.Multitenancy;

public class CurrentBoxAccessor : ICurrentBoxAccessor
{
    private static readonly AsyncLocal<Guid?> Current = new();

    // ponytail: EF Core's query translator evaluates captured-variable member accesses (like this
    // getter) eagerly as parameter values when building a query — it does NOT respect the `||`
    // short-circuit in `!currentBox.HasBox || e.BoxId == currentBox.BoxId` at the .NET level, only
    // at the SQL level. So this must never throw, or every unscoped (anonymous) query blows up
    // before the "no box scope -> unfiltered" branch even gets a chance to apply.
    public Guid BoxId => Current.Value ?? Guid.Empty;
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
