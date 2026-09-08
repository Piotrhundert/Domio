namespace Domio.Application.Auditing;

public sealed record AuditEntry(
    string EventType,
    string EntityType,
    string? EntityId,
    string? ActorId,
    string CorrelationId,
    string? Description = null,
    string? OldValuesJson = null,
    string? NewValuesJson = null);
