namespace Domio.Domain.Common;

public sealed class DatabaseMetadataRecord
{
    public int Id { get; set; }

    public string InstanceId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
