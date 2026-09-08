namespace Domio.Application.Maintenance;

public sealed record DatabaseMaintenanceRuntime(
    string EnvironmentName,
    string ContentRootPath);

public sealed record DatabaseBackupResult(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc);

public sealed record DatabaseRestoreResult(
    string FileName,
    DateTime RestoredAtUtc);

public sealed record TestDatabaseResetResult(
    int SchemaVersion,
    string InstanceId,
    DateTime ResetAtUtc);
