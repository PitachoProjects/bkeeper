using BKeeper.Infrastructure.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BKeeper.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef migrations add` works without spinning up the full DI container.</summary>
public class BKeeperDbContextFactory : IDesignTimeDbContextFactory<BKeeperDbContext>
{
    public BKeeperDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BKEEPER_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=bkeeper;Username=bkeeper;Password=bkeeper";

        var options = new DbContextOptionsBuilder<BKeeperDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BKeeperDbContext(options, new CurrentBoxAccessor());
    }
}
