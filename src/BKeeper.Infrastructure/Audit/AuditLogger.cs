using BKeeper.Domain.Entities;
using BKeeper.Infrastructure.Persistence;

namespace BKeeper.Infrastructure.Audit;

public class AuditLogger(BKeeperDbContext db)
{
    /// <summary>Queues an audit row (call db.SaveChangesAsync afterward, same as any other tracked entity).</summary>
    public void Log(Guid boxId, Guid? actorUserId, string action, string target) =>
        db.AuditLogs.Add(new AuditLog { BoxId = boxId, ActorUserId = actorUserId, Action = action, Target = target });
}
