using Domio.Application.Authentication;
using Domio.Application.Notifications;
using Domio.Domain.Notifications;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_10_1_NotificationCenterTests
{
    [Fact]
    public async Task Notification_center_should_dedupe_mark_read_and_keep_activity_history()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-10-1-notifications-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            root);

        var databasePath =
            Path.Combine(
                root,
                "domio-test.db");

        try
        {
            var options =
                new DbContextOptionsBuilder<DomioDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                    .Options;

            await using var dbContext =
                new DomioDbContext(
                    options);

            await dbContext.Database.MigrateAsync();

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.InRange(
                schemaVersion,
                26,
                int.MaxValue);

            Assert.Empty(
                await dbContext.Database
                    .GetPendingMigrationsAsync());

            var authenticationService =
                new AccountAuthenticationService(
                    dbContext,
                    new Domio.Infrastructure.Auditing.AuditService(
                        dbContext));

            var administrator =
                await authenticationService
                    .InitializeFirstAdministratorAsync(
                        new FirstAdministratorSetupRequest(
                            "Jan",
                            "Administrator",
                            "admin",
                            "DomioTest123"),
                        Guid.NewGuid()
                            .ToString("N"));

            var service =
                new NotificationService(
                    dbContext);

            var request =
                new PublishNotificationRequest(
                    [administrator.UserId],
                    "Test.Invoice.Created",
                    NotificationCategoryCodes.Invoice,
                    NotificationSeverityCodes.Important,
                    "Nowa faktura",
                    "Dodano fakturę testową.",
                    true,
                    "/HouseholdFinance/Invoices",
                    "HouseholdInvoice",
                    Guid.NewGuid().ToString(),
                    "test:invoice:1");

            await service.PublishAsync(
                request);

            await service.PublishAsync(
                request);

            var overview =
                await service.GetOverviewAsync(
                    administrator.UserId);

            var invoiceNotification =
                Assert.Single(
                    overview.Inbox.Where(x =>
                        x.EventCode ==
                        "Test.Invoice.Created"));

            Assert.False(
                invoiceNotification.IsRead);

            await service.MarkReadAsync(
                invoiceNotification.NotificationId,
                administrator.UserId);

            var afterRead =
                await service.GetOverviewAsync(
                    administrator.UserId);

            var readInvoice =
                Assert.Single(
                    afterRead.Inbox.Where(x =>
                        x.EventCode ==
                        "Test.Invoice.Created"));

            Assert.True(
                readInvoice.IsRead);

            Assert.Contains(
                afterRead.ActivityHistory,
                x =>
                    x.NotificationId ==
                    invoiceNotification.NotificationId);

            await service.MarkAllReadAsync(
                administrator.UserId);

            var header =
                await service.GetHeaderAsync(
                    administrator.UserId);

            Assert.Equal(
                0,
                header.UnreadCount);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
