namespace Domio.Application.Maintenance;

public interface IDatabaseMaintenanceService
{
    Task<DatabaseBackupResult> CreateBackupAsync(
        CancellationToken cancellationToken = default);

    Task<DatabaseRestoreResult> RestoreBackupAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    Task<TestDatabaseResetResult> ResetTestDatabaseAsync(
        CancellationToken cancellationToken = default);
}
