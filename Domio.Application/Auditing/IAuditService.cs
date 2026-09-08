namespace Domio.Application.Auditing;

public interface IAuditService
{
    Task<Guid> WriteAsync(
        AuditEntry entry,
        CancellationToken cancellationToken = default);
}
