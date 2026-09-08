using System.Data;
using System.Data.Common;
using Domio.Application.Diagnostics;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Diagnostics;

public sealed class DatabaseDiagnosticsService(
    DomioDbContext dbContext) : IDatabaseDiagnosticsService
{
    public async Task<DatabaseDiagnosticsResult> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database
            .CanConnectAsync(cancellationToken);

        if (!canConnect)
        {
            return new DatabaseDiagnosticsResult(
                CanConnect: false,
                IntegrityCheck: "unavailable",
                JournalMode: "unavailable",
                ForeignKeysEnabled: false,
                SchemaVersion: 0,
                InstanceId: string.Empty,
                AppliedMigrations: 0,
                PendingMigrations: 0,
                CurrentMigration: null);
        }

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

        var applied = (await dbContext.Database
                .GetAppliedMigrationsAsync(cancellationToken))
            .ToArray();

        var pending = (await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken))
            .ToArray();

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var integrityCheck = await ExecuteScalarAsync(
                connection,
                "PRAGMA integrity_check;",
                cancellationToken);

            var journalMode = await ExecuteScalarAsync(
                connection,
                "PRAGMA journal_mode;",
                cancellationToken);

            var foreignKeys = await ExecuteScalarAsync(
                connection,
                "PRAGMA foreign_keys;",
                cancellationToken);

            return new DatabaseDiagnosticsResult(
                CanConnect: true,
                IntegrityCheck: integrityCheck,
                JournalMode: journalMode,
                ForeignKeysEnabled: foreignKeys == "1",
                SchemaVersion: schemaVersion,
                InstanceId: instanceId,
                AppliedMigrations: applied.Length,
                PendingMigrations: pending.Length,
                CurrentMigration: applied.LastOrDefault());
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<string> ExecuteScalarAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToString(
                   result,
                   System.Globalization.CultureInfo.InvariantCulture)
               ?? string.Empty;
    }
}
