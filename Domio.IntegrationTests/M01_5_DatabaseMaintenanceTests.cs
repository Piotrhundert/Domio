using Domio.Application.Auditing;
using Domio.Application.Maintenance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Maintenance;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Domio.IntegrationTests;

public sealed class M01_5_DatabaseMaintenanceTests
{
    [Fact]
    public async Task Backup_restore_and_test_reset_should_be_safe_and_repeatable()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m01-5-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);

        var databasePath =
            Path.Combine(root, "domio-test.db");

        try
        {
            var configuration =
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Maintenance:BackupDirectory"] =
                                "backups",
                            ["Maintenance:TestDatabaseFileName"] =
                                "domio-test.db"
                        })
                    .Build();

            var runtime =
                new DatabaseMaintenanceRuntime(
                    EnvironmentName: "Test",
                    ContentRootPath: root);

            var options =
                new DbContextOptionsBuilder<DomioDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                    .Options;

            string originalInstanceId;

            await using (var dbContext =
                         new DomioDbContext(options))
            {
                await dbContext.Database.MigrateAsync();

                originalInstanceId =
                    await dbContext.DatabaseMetadata
                        .AsNoTracking()
                        .Where(x => x.Id == 1)
                        .Select(x => x.InstanceId)
                        .SingleAsync();

                var auditService =
                    new AuditService(dbContext);

                await auditService.WriteAsync(
                    new AuditEntry(
                        EventType: "M01.5.BeforeBackup",
                        EntityType: "System",
                        EntityId: "test",
                        ActorId: "integration-test",
                        CorrelationId:
                            Guid.NewGuid().ToString("N")));

                var maintenance =
                    new DatabaseMaintenanceService(
                        dbContext,
                        configuration,
                        runtime);

                var backup =
                    await maintenance.CreateBackupAsync();

                Assert.True(backup.SizeBytes > 0);
                Assert.True(
                    File.Exists(
                        Path.Combine(
                            root,
                            "backups",
                            backup.FileName)));

                await auditService.WriteAsync(
                    new AuditEntry(
                        EventType: "M01.5.AfterBackup",
                        EntityType: "System",
                        EntityId: "test",
                        ActorId: "integration-test",
                        CorrelationId:
                            Guid.NewGuid().ToString("N")));

                Assert.Equal(
                    2,
                    await dbContext.AuditLogs.CountAsync());

                await maintenance.RestoreBackupAsync(
                    backup.FileName);
            }

            await using (var verificationContext =
                         new DomioDbContext(options))
            {
                Assert.Equal(
                    1,
                    await verificationContext.AuditLogs
                        .CountAsync());

                var maintenance =
                    new DatabaseMaintenanceService(
                        verificationContext,
                        configuration,
                        runtime);

                var reset =
                    await maintenance.ResetTestDatabaseAsync();

                Assert.True(reset.SchemaVersion >= 2);
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        reset.InstanceId));
                Assert.NotEqual(
                    originalInstanceId,
                    reset.InstanceId);

                Assert.Equal(
                    0,
                    await verificationContext.AuditLogs
                        .CountAsync());
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task Reset_should_be_blocked_outside_test_environment()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m01-5-guard-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);

        var databasePath =
            Path.Combine(root, "domio-test.db");

        try
        {
            var configuration =
                new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Maintenance:BackupDirectory"] =
                                "backups",
                            ["Maintenance:TestDatabaseFileName"] =
                                "domio-test.db"
                        })
                    .Build();

            var runtime =
                new DatabaseMaintenanceRuntime(
                    EnvironmentName: "Development",
                    ContentRootPath: root);

            var options =
                new DbContextOptionsBuilder<DomioDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                    .Options;

            await using var dbContext =
                new DomioDbContext(options);

            await dbContext.Database.MigrateAsync();

            var maintenance =
                new DatabaseMaintenanceService(
                    dbContext,
                    configuration,
                    runtime);

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => maintenance.ResetTestDatabaseAsync());

            Assert.Contains(
                "wyłącznie w środowisku Test",
                exception.Message);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
