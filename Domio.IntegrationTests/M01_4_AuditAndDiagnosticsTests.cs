using Domio.Application.Auditing;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Diagnostics;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M01_4_AuditAndDiagnosticsTests
{
    [Fact]
    public async Task Audit_and_database_diagnostics_should_be_healthy()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"domio-m01-4-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<DomioDbContext>()
                .UseSqlite(
                    $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                .Options;

            await using (var dbContext = new DomioDbContext(options))
            {
                await dbContext.Database.MigrateAsync();

                var auditService = new AuditService(dbContext);

                var auditId = await auditService.WriteAsync(
                    new AuditEntry(
                        EventType: "M01.4.Test",
                        EntityType: "System",
                        EntityId: "integration-test",
                        ActorId: "test",
                        CorrelationId: Guid.NewGuid().ToString("N"),
                        Description: "Test zapisu audytu."));

                var storedAudit = await dbContext.AuditLogs
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == auditId);

                Assert.Equal("M01.4.Test", storedAudit.EventType);

                var diagnosticsService =
                    new DatabaseDiagnosticsService(dbContext);

                var diagnostics =
                    await diagnosticsService.GetAsync();

                Assert.True(diagnostics.IsHealthy);
                Assert.True(diagnostics.CanConnect);
                Assert.Equal("ok", diagnostics.IntegrityCheck);
                Assert.True(diagnostics.ForeignKeysEnabled);
                Assert.True(diagnostics.SchemaVersion >= 2);
                Assert.Equal(0, diagnostics.PendingMigrations);
                Assert.False(string.IsNullOrWhiteSpace(
                    diagnostics.InstanceId));
                Assert.False(string.IsNullOrWhiteSpace(
                    diagnostics.CurrentMigration));

                await dbContext.Database.CloseConnectionAsync();
            }

            await Task.Delay(100);
        }
        finally
        {
            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-shm");
            DeleteIfExists(databasePath + "-wal");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
