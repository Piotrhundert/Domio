namespace Domio.Domain.Audit;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? ActorId { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
