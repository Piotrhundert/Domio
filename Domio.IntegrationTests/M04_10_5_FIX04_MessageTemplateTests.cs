using System.Data;
using Domio.Application.Authentication;
using Domio.Application.Notifications;
using Domio.Domain.Notifications;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Notifications;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_10_5_FIX04_MessageTemplateTests
{
    [Fact]
    public async Task Category_template_should_render_subject_and_body_and_support_reset()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-10-5-fix04-{Guid.NewGuid():N}");

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
                29,
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

            var settingsService =
                new NotificationSettingsService(
                    dbContext,
                    new NotificationSecretProtector(
                        root));

            await settingsService.SaveUserSettingsAsync(
                administrator.UserId,
                new UpdateNotificationUserSettingsRequest(
                    InAppEnabled: true,
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
                    ApplicationBaseUrl: "https://domio.home",
                    PollIntervalMinutes: 1));

            await settingsService.SaveMessageTemplateAsync(
                administrator.UserId,
                new UpdateNotificationMessageTemplateRequest(
                    NotificationCategoryCodes.Invoice,
                    "FV · {Title} · {Category}",
                    "Wiadomość: {Message}\n{Link}\nKod: {EventCode}"));

            var templates =
                await settingsService.GetMessageTemplatesAsync(
                    administrator.UserId);

            var invoiceTemplate =
                Assert.Single(
                    templates.Where(x =>
                        x.CategoryCode ==
                        NotificationCategoryCodes.Invoice));

            Assert.True(
                invoiceTemplate.IsCustomized);

            var notificationService =
                new NotificationService(
                    dbContext);

            await notificationService.PublishAsync(
                new PublishNotificationRequest(
                    [administrator.UserId],
                    "M04.10.5.FIX04.InvoiceTemplateTest",
                    NotificationCategoryCodes.Invoice,
                    NotificationSeverityCodes.Important,
                    "Faktura testowa",
                    "Do zapłaty 10,00 zł.",
                    true,
                    "/HouseholdFinance/Invoices",
                    "HouseholdInvoice",
                    Guid.NewGuid().ToString(),
                    $"m04.10.5:fix04:{Guid.NewGuid():N}"));

            var orchestrator =
                new NotificationDeliveryOrchestratorService(
                    dbContext,
                    notificationService,
                    settingsService);

            await orchestrator.ProcessUserAsync(
                administrator.UserId);

            var deliveries =
                await settingsService.GetRecentEmailDeliveriesAsync(
                    administrator.UserId,
                    20);

            var delivery =
                Assert.Single(
                    deliveries.Where(x =>
                        x.Subject.Contains(
                            "Faktura testowa",
                            StringComparison.Ordinal)));

            Assert.Equal(
                "FV · Faktura testowa · Faktury",
                delivery.Subject);

            string? body = null;

            var connection =
                dbContext.Database.GetDbConnection();

            var shouldClose =
                connection.State !=
                ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    """
                    SELECT Body
                    FROM NotificationEmailDeliveries
                    WHERE Id = $id;
                    """;

                var parameter =
                    command.CreateParameter();

                parameter.ParameterName =
                    "$id";

                parameter.Value =
                    delivery.DeliveryId;

                command.Parameters.Add(
                    parameter);

                body =
                    Convert.ToString(
                        await command.ExecuteScalarAsync());
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            Assert.NotNull(
                body);

            Assert.True(
                body!.Contains(
                    "Wiadomość: Do zapłaty 10,00 zł.",
                    StringComparison.Ordinal));

            Assert.True(
                body.Contains(
                    "https://domio.home/HouseholdFinance/Invoices",
                    StringComparison.Ordinal));

            await settingsService.ResetMessageTemplateAsync(
                administrator.UserId,
                NotificationCategoryCodes.Invoice);

            templates =
                await settingsService.GetMessageTemplatesAsync(
                    administrator.UserId);

            invoiceTemplate =
                Assert.Single(
                    templates.Where(x =>
                        x.CategoryCode ==
                        NotificationCategoryCodes.Invoice));

            Assert.False(
                invoiceTemplate.IsCustomized);

            Assert.Equal(
                NotificationMessageTemplateDefaults.SubjectTemplate,
                invoiceTemplate.SubjectTemplate);
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
