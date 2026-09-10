using Domio.Application.Authentication;
using Domio.Application.PersonalFinance;
using Domio.Application.Users;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M03_3_RecurringPersonalFinanceTests
{
    [Fact]
    public async Task Recurring_salary_and_subscription_should_create_idempotent_plans_without_changing_balance()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-3-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);

        var databasePath =
            Path.Combine(root, "domio-test.db");

        try
        {
            var options =
                new DbContextOptionsBuilder<DomioDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                    .Options;

            await using var dbContext =
                new DomioDbContext(options);

            await dbContext.Database.MigrateAsync();

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.True(schemaVersion >= 9);
            Assert.Empty(
                await dbContext.Database
                    .GetPendingMigrationsAsync());

            var auditService =
                new AuditService(dbContext);

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

            var userManagement =
                new UserManagementService(
                    dbContext,
                    auditService);

            var userId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Anna",
                        "Domownik",
                        null,
                        null,
                        "anna@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var service =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var accountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto główne",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        1000m),
                    userId,
                    Guid.NewGuid().ToString("N"));

            var salaryStart =
                new DateTime(
                    DateTime.UtcNow.Year,
                    DateTime.UtcNow.Month,
                    10,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            await service.CreateOwnRecurringRuleAsync(
                new CreatePersonalRecurringRuleRequest(
                    accountId,
                    PersonalTransactionKinds.Income,
                    "Wynagrodzenie",
                    5800m,
                    PersonalRecurringFrequencies.Monthly,
                    PersonalFinanceCategories.Salary,
                    "Firma ABC",
                    salaryStart,
                    null),
                userId,
                Guid.NewGuid().ToString("N"));

            await service.CreateOwnRecurringRuleAsync(
                new CreatePersonalRecurringRuleRequest(
                    accountId,
                    PersonalTransactionKinds.Expense,
                    "Streaming",
                    43m,
                    PersonalRecurringFrequencies.Monthly,
                    PersonalFinanceCategories.Subscription,
                    "Streaming Test",
                    salaryStart.AddDays(5),
                    null),
                userId,
                Guid.NewGuid().ToString("N"));

            var firstOverview =
                await service.GetOwnOverviewAsync(userId);

            Assert.Equal(
                1000m,
                firstOverview.Accounts
                    .Single()
                    .Balance);

            Assert.Equal(
                2,
                firstOverview.RecurringRules.Count);

            Assert.Contains(
                firstOverview.RecurringRules,
                x =>
                    x.Name == "Wynagrodzenie" &&
                    x.PlannedAmount == 5800m &&
                    x.Counterparty == "Firma ABC");

            Assert.Contains(
                firstOverview.RecurringRules,
                x =>
                    x.Name == "Streaming" &&
                    x.PlannedAmount == 43m);

            var occurrenceCountBefore =
                await dbContext.PersonalRecurringOccurrences
                    .CountAsync();

            _ = await service.GetOwnOverviewAsync(userId);
            _ = await service.GetOwnOverviewAsync(userId);

            var occurrenceCountAfter =
                await dbContext.PersonalRecurringOccurrences
                    .CountAsync();

            Assert.Equal(
                occurrenceCountBefore,
                occurrenceCountAfter);

            var duplicatePeriods =
                await dbContext.PersonalRecurringOccurrences
                    .GroupBy(x => new
                    {
                        x.RecurringRuleId,
                        x.PeriodKey
                    })
                    .Where(x => x.Count() > 1)
                    .CountAsync();

            Assert.Equal(
                0,
                duplicatePeriods);

            Assert.Equal(
                1000m,
                (await service.GetOwnOverviewAsync(userId))
                    .Accounts
                    .Single()
                    .Balance);

            var auditEntries =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.EventType ==
                        "M03.3.PersonalRecurringRuleCreated")
                    .ToListAsync();

            Assert.Equal(2, auditEntries.Count);

            Assert.All(
                auditEntries,
                x =>
                {
                    var text =
                        string.Join(
                            " ",
                            x.Description,
                            x.OldValuesJson,
                            x.NewValuesJson);

                    Assert.DoesNotContain("5800", text);
                    Assert.DoesNotContain("43", text);
                    Assert.DoesNotContain("Firma ABC", text);
                });
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
