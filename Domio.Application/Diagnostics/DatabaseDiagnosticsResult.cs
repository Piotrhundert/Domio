namespace Domio.Application.Diagnostics;

public sealed record DatabaseDiagnosticsResult(
    bool CanConnect,
    string IntegrityCheck,
    string JournalMode,
    bool ForeignKeysEnabled,
    int SchemaVersion,
    string InstanceId,
    int AppliedMigrations,
    int PendingMigrations,
    string? CurrentMigration)
{
    public bool IsHealthy =>
        CanConnect &&
        string.Equals(IntegrityCheck, "ok", StringComparison.OrdinalIgnoreCase) &&
        ForeignKeysEnabled &&
        PendingMigrations == 0;
}
