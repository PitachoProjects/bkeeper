using BKeeper.Application.Abstractions;
using BKeeper.Application.Import;
using BKeeper.Infrastructure.Auth;
using BKeeper.Infrastructure.Identity;
using BKeeper.Infrastructure.Import;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using BKeeper.Infrastructure.Pipeline;
using BKeeper.Infrastructure.Rules;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BKeeper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBKeeperInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<BKeeperDbContext>(opt => opt.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>(opt =>
            {
                opt.Password.RequiredLength = 8;
                opt.Password.RequireNonAlphanumeric = false;
                opt.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<BKeeperDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        services.AddSingleton<CurrentBoxAccessor>();
        services.AddSingleton<ICurrentBoxAccessor>(sp => sp.GetRequiredService<CurrentBoxAccessor>());

        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddBKeeperRules();
        services.AddScoped<DailyRulePipeline>();

#pragma warning disable CS0618 // simple string overload is obsolete in 1.20 in favor of an options-action; fine for now
        services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(connectionString));
#pragma warning restore CS0618
        services.AddHangfireServer();

        return services;
    }
}
