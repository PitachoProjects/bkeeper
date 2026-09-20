using BKeeper.Application.Rules;
using BKeeper.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace BKeeper.Infrastructure.Rules;

public static class RuleRegistration
{
    public static IServiceCollection AddBKeeperRules(this IServiceCollection services)
    {
        services.AddSingleton<IRule, R01AbsenceGap>();
        services.AddSingleton<IRule, R02NotBooking>();
        services.AddSingleton<IRule, R03FrequencyDrop>();
        services.AddSingleton<IRule, R04NoShowStreak>();
        services.AddSingleton<IRule, R08OnboardingNoFirstVisit>();
        return services;
    }
}
