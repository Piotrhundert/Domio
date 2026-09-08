using Domio.Application.Auditing;
using Domio.Domain.Audit;
using Domio.Infrastructure.Persistence;

namespace Domio.Infrastructure.Auditing;

public sealed class AuditService(
    DomioDbContext dbContext) : IAuditService
{
    public async Task<Guid> WriteAsync(
        AuditEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.EventType))
        {
            throw new ArgumentException(
                "EventType audytu nie może być pusty.",
                nameof(entry));
        }

        if (string.IsNullOrWhiteSpace(entry.EntityType))
        {
            throw new ArgumentException(
                "EntityType audytu nie może być pusty.",
                nameof(entry));
        }

        if (string.IsNullOrWhiteSpace(entry.CorrelationId))
        {
            throw new ArgumentException(
                "CorrelationId audytu nie może być pusty.",
                nameof(entry));
        }

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            EventType = entry.EventType.Trim(),
            EntityType = entry.EntityType.Trim(),
            EntityId = Normalize(entry.EntityId),
            ActorId = Normalize(entry.ActorId),
            CorrelationId = entry.CorrelationId.Trim(),
            Description = Normalize(entry.Description),
            OldValuesJson = Normalize(entry.OldValuesJson),
            NewValuesJson = Normalize(entry.NewValuesJson),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        return auditLog.Id;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
