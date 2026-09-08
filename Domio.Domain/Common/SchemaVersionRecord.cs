namespace Domio.Domain.Common;

public sealed class SchemaVersionRecord
{
    public int Id { get; set; }

    public int Version { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
