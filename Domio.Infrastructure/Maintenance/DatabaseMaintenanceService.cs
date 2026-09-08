using Domio.Application.Maintenance;
using Domio.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Domio.Infrastructure.Maintenance;

public sealed class DatabaseMaintenanceService(
    DomioDbContext dbContext,
    IConfiguration configuration,
    DatabaseMaintenanceRuntime runtime) : IDatabaseMaintenanceService
{
    private const string DefaultBackupDirectory = "backups";
    private const string DefaultTestDatabaseFileName = "domio-test.db";

    public async Task<DatabaseBackupResult> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        var backupDirectory = GetBackupDirectory();
        Directory.CreateDirectory(backupDirectory);

        var fileName =
            $"domio-backup-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.db";

        var backupPath = Path.Combine(
            backupDirectory,
            fileName);

        var sourceConnectionString =
            GetConnectionString(pooling: false);

        var destinationBuilder =
            new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                ForeignKeys = true
            };

        await using var source =
            new SqliteConnection(sourceConnectionString);

        await using var destination =
            new SqliteConnection(destinationBuilder.ToString());

        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);

        source.BackupDatabase(destination);

        await destination.CloseAsync();
        await source.CloseAsync();

        var fileInfo = new FileInfo(backupPath);

        return new DatabaseBackupResult(
            FileName: fileName,
            SizeBytes: fileInfo.Length,
            CreatedAtUtc: DateTime.UtcNow);
    }

    public async Task<DatabaseRestoreResult> RestoreBackupAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        EnsureRestoreEnvironment();

        var backupPath = ResolveBackupPath(fileName);

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException(
                "Nie znaleziono wskazanego backupu Domio.",
                fileName);
        }

        await dbContext.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        var sourceBuilder =
            new SqliteConnectionStringBuilder
            {
                DataSource = backupPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                ForeignKeys = true
            };

        var destinationConnectionString =
            GetConnectionString(pooling: false);

        await using var source =
            new SqliteConnection(sourceBuilder.ToString());

        await using var destination =
            new SqliteConnection(destinationConnectionString);

        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);

        source.BackupDatabase(destination);

        await destination.CloseAsync();
        await source.CloseAsync();

        SqliteConnection.ClearAllPools();

        return new DatabaseRestoreResult(
            FileName: fileName,
            RestoredAtUtc: DateTime.UtcNow);
    }

    public async Task<TestDatabaseResetResult> ResetTestDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureTestResetIsAllowed();

        await dbContext.Database.CloseConnectionAsync();
        SqliteConnection.ClearAllPools();

        await dbContext.Database.EnsureDeletedAsync(
            cancellationToken);

        await dbContext.Database.MigrateAsync(
            cancellationToken);

        var schemaVersion = await dbContext.SchemaVersions
            .AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => x.Version)
            .SingleAsync(cancellationToken);

        var instanceId = await dbContext.DatabaseMetadata
            .AsNoTracking()
            .Where(x => x.Id == 1)
            .Select(x => x.InstanceId)
            .SingleAsync(cancellationToken);

        return new TestDatabaseResetResult(
            SchemaVersion: schemaVersion,
            InstanceId: instanceId,
            ResetAtUtc: DateTime.UtcNow);
    }

    private string GetConnectionString(bool pooling)
    {
        var connectionString =
            dbContext.Database.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Brak connection string bazy Domio.");
        }

        var builder =
            new SqliteConnectionStringBuilder(
                connectionString)
            {
                Pooling = pooling,
                ForeignKeys = true
            };

        return builder.ToString();
    }

    private string GetBackupDirectory()
    {
        var configured =
            configuration["Maintenance:BackupDirectory"];

        var relativeOrAbsolute =
            string.IsNullOrWhiteSpace(configured)
                ? DefaultBackupDirectory
                : configured.Trim();

        return Path.GetFullPath(
            Path.IsPathRooted(relativeOrAbsolute)
                ? relativeOrAbsolute
                : Path.Combine(
                    runtime.ContentRootPath,
                    relativeOrAbsolute));
    }

    private string ResolveBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException(
                "Nazwa pliku backupu nie może być pusta.",
                nameof(fileName));
        }

        var safeFileName = Path.GetFileName(fileName);

        if (!string.Equals(
                safeFileName,
                fileName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Nieprawidłowa nazwa pliku backupu.");
        }

        return Path.Combine(
            GetBackupDirectory(),
            safeFileName);
    }

    private void EnsureRestoreEnvironment()
    {
        if (!IsDevelopment() && !IsTest())
        {
            throw new InvalidOperationException(
                "Restore bazy jest dostępny tylko w środowisku Development lub Test.");
        }
    }

    private void EnsureTestResetIsAllowed()
    {
        if (!IsTest())
        {
            throw new InvalidOperationException(
                "Destrukcyjny reset jest dozwolony wyłącznie w środowisku Test.");
        }

        var configuredTestFile =
            configuration["Maintenance:TestDatabaseFileName"];

        var expectedFileName =
            string.IsNullOrWhiteSpace(configuredTestFile)
                ? DefaultTestDatabaseFileName
                : configuredTestFile.Trim();

        var connectionBuilder =
            new SqliteConnectionStringBuilder(
                GetConnectionString(pooling: false));

        var actualFileName = Path.GetFileName(
            connectionBuilder.DataSource);

        if (!string.Equals(
                actualFileName,
                expectedFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Reset zablokowany: aktywna baza '{actualFileName}' nie jest bazą testową '{expectedFileName}'.");
        }
    }

    private bool IsDevelopment() =>
        string.Equals(
            runtime.EnvironmentName,
            "Development",
            StringComparison.OrdinalIgnoreCase);

    private bool IsTest() =>
        string.Equals(
            runtime.EnvironmentName,
            "Test",
            StringComparison.OrdinalIgnoreCase);
}
