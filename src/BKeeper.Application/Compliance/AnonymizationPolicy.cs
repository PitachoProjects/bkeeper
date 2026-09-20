using BKeeper.Domain.Enums;

namespace BKeeper.Application.Compliance;

/// <summary>Plan §11/§14 example: "cancelled members anonymised after 24 months." Pure so the cutoff logic is testable without a database.</summary>
public static class AnonymizationPolicy
{
    public const int DefaultRetentionMonths = 24;

    public static bool ShouldAnonymize(MemberStatus status, DateOnly? cancelDate, DateOnly asOf, int retentionMonths = DefaultRetentionMonths)
    {
        if (status != MemberStatus.Cancelled || cancelDate is null) return false;
        var months = (asOf.Year - cancelDate.Value.Year) * 12 + asOf.Month - cancelDate.Value.Month;
        return months >= retentionMonths;
    }
}
