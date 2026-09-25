using Domio.Application.Authentication;
using Domio.Application.Notifications;
using Domio.Domain.Notifications;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_10_5_NotificationSettingsAndEmailTests
{
    [Fact]
    public async Task Settings_should_keep_activity_hide_in_app_and_queue_email()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-10-5-{Guid.NewGuid():N}");

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
                27,
                int.MaxValue);

            var auditService =
                new AuditService(
                    dbContext);

            var authenticationService =
                new AccountAuthenticationService(
                    dbContext,
                    auditService);

            var administrator =
                await authenticationService
                    .InitializeFirstAdministratorAsync(
                        new FirstAdministratorSetupRequest(
                            "Jan",
                            "Administrator",
                            "admin",
                            "DomioTest123"),
                        Guid.NewGuid().ToString("N"));

            var secretProtector =
                new NotificationSecretProtector(
                    root);

            var settingsService =
                new NotificationSettingsService(
                    dbContext,
                    secretProtector);

            await settingsService.SaveUserSettingsAsync(
                administrator.UserId,
                new UpdateNotificationUserSettingsRequest(
                    InAppEnabled: false,
                    EmailEnabled: true,
                    EmailAddress: "jan@example.com",
                    Reminder7Days: true,
                    Reminder3Days: true,
                    Reminder1Day: true,
                    ReminderDueDay: true,
                    ReminderOverdue: true,
                    NotifySystem: true,
                    NotifyPersonalFinance: true,
                    NotifyHousehold: true,
                    NotifyContributions: true,
                    NotifyInvoices: true,
                    NotifyFamily: true,
                    NotifyGoals: true,
                    NotifyUsers: true));

            await settingsService.SaveEmailConfigurationAsync(
                administrator.UserId,
                new UpdateNotificationEmailConfigurationRequest(
                    IsEnabled: true,
                    SenderName: "Domio",
                    SenderEmail: "domio@example.com",
                    SmtpHost: "smtp.invalid.example",
                    SmtpPort: 587,
                    SmtpUsername: string.Empty,
                    SmtpPassword: null,
                    UseSsl: true,
                    ApplicationBaseUrl: "https://domio.home"));

            var notificationService =
                new NotificationService(
                    dbContext);

            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [administrator.UserId],
                    "M04.10.5.TestInvoice",
                    NotificationCategoryCodes.Invoice,
                    NotificationSeverityCodes.Important,
                    "Test ustawień",
                    "To zdarzenie ma zostać w historii i trafić do kolejki e-mail.",
                    true,
                    "/HouseholdFinance/Invoices",
                    "TestInvoice",
                    Guid.NewGuid().ToString(),
                    $"m04.10.5:test:{Guid.NewGuid():N}"));

            var orchestrator =
                new NotificationDeliveryOrchestratorService(
                    dbContext,
                    notificationService,
                    settingsService);

            await orchestrator.ProcessUserAsync(
                administrator.UserId);

            var overview =
                await notificationService.GetOverviewAsync(
                    administrator.UserId);

            Assert.DoesNotContain(
                overview.Inbox,
                x =>
                    x.EventCode ==
                    "M04.10.5.TestInvoice");

            Assert.Contains(
                overview.ActivityHistory,
                x =>
                    x.EventCode ==
                    "M04.10.5.TestInvoice");

            var deliveries =
                await settingsService
                    .GetRecentEmailDeliveriesAsync(
                        administrator.UserId,
                        20);

            Assert.Contains(
                deliveries,
                x =>
                    x.RecipientEmail ==
                    "jan@example.com" &&
                    x.Subject.Contains(
                        "Test ustawień",
                        StringComparison.Ordinal) &&
                    x.StatusCode ==
                    NotificationEmailDeliveryStatuses.Pending);
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
