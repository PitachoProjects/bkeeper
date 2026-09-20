using BKeeper.Infrastructure.Multitenancy;
using Xunit;

namespace BKeeper.Tests.Unit.Multitenancy;

public class CurrentBoxAccessorTests
{
    [Fact]
    public void BoxId_WhenUnscoped_DoesNotThrow()
    {
        // Regression test: EF Core evaluates `currentBox.BoxId` as a query parameter even when the
        // filter is `!currentBox.HasBox || e.BoxId == currentBox.BoxId` — the C# `||` short-circuit
        // doesn't protect this getter, so it must return a safe default instead of throwing.
        var accessor = new CurrentBoxAccessor();
        Assert.False(accessor.HasBox);
        var boxId = Record.Exception(() => accessor.BoxId);
        Assert.Null(boxId);
        Assert.Equal(Guid.Empty, accessor.BoxId);
    }

    [Fact]
    public void Use_ScopesBoxIdForTheDurationOfTheBlock()
    {
        var accessor = new CurrentBoxAccessor();
        var boxId = Guid.NewGuid();

        using (accessor.Use(boxId))
        {
            Assert.True(accessor.HasBox);
            Assert.Equal(boxId, accessor.BoxId);
        }

        Assert.False(accessor.HasBox);
    }
}
