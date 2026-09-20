using BKeeper.Application.Compliance;
using BKeeper.Domain.Enums;
using BKeeper.Infrastructure.Audit;
using BKeeper.Infrastructure.Multitenancy;
using BKeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BKeeper.Infrastructure.Pipeline;

/// <summary>Daily job implementing plan §11/§14's retention example: anonymize members cancelled 24+ months ago.</summary>
public class AnonymizationJob(BKeeperDbContext db, CurrentBoxAccessor currentBox, AuditLogger audit, ILogger<AnonymizationJob> logger)
{
    public async Task RunForAllBoxesAsync(DateOnly asOf, CancellationToken ct = default)
    {
        var boxIds = await db.Boxes.Select(b => b.Id).ToListAsync(ct);
        foreach (var boxId in boxIds)
        {
            using (currentBox.Use(boxId))
            {
                await RunForBoxAsync(boxId, asOf, ct);
            }
        }
    }

    public async Task<int> RunForBoxAsync(Guid boxId, DateOnly asOf, CancellationToken ct = default)
    {
        var candidates = await db.Members
            .Where(m => m.Status == MemberStatus.Cancelled && m.CancelDate != null)
            .Where(m => m.Name != "Anonymized member") // idempotent: skip already-anonymized rows
            .ToListAsync(ct);

        var anonymized = 0;
        foreach (var member in candidates)
        {
            if (!AnonymizationPolicy.ShouldAnonymize(member.Status, member.CancelDate, asOf)) continue;

            member.Name = "Anonymized member";
            member.Email = null;
            member.PhoneE164 = null;
            member.BirthYear = null;
            member.UpdatedAt = DateTimeOffset.UtcNow;
            audit.Log(boxId, null, "anonymize", $"member:{member.Id}");
            anonymized++;
        }

        if (anonymized > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Anonymization job for box {BoxId}: {Count} members anonymized", boxId, anonymized);
        }

        return anonymized;
    }
}
