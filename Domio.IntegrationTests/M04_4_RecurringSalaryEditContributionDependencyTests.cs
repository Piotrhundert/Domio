using Domio.Application.Authentication;
using Domio.Application.HouseholdFinance;
using Domio.Application.PersonalFinance;
using Domio.Application.Users;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_4_RecurringSalaryEditContributionDependencyTests
{
    [Fact]
    public async Task Editing_current_planned_salary_should_rebuild_unpaid_contribution_without_fk_error()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-4-salary-edit-{Guid.NewGuid():N}");

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

            await dbContext.Database
                .MigrateAsync();

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x =>
                        x.Id == 1)
                    .Select(x =>
                        x.Version)
                    .SingleAsync();

            Assert.True(
                schemaVersion >= 14,
                $"Oczekiwano schemaVersion >= 14, otrzymano {schemaVersion}.");

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

            var userManagement =
                new UserManagementService(
                    dbContext,
                    auditService);

            var memberUserId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Piotr",
                        "Domownik",
                        null,
                        null,
                        "piotr-salary-edit@example.test",
                        "DomioPiotr123",
                        SystemRoles.HouseholdMemberId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var householdService =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var householdAccountId =
                await householdService.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Konto Domowe",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var personalService =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var privateAccountId =
                await personalService.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Bank_Piotr",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        0m),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            var today =
                DateTime.UtcNow.Date;

            var salaryRuleId =
                await personalService.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        privateAccountId,
                        PersonalTransactionKinds.Income,
                        "wynagrodzenie",
                        3000m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Pracodawca",
                        today,
                        null),
                    memberUserId,
                    Guid.NewGuid().ToString("N"));

            var contributionBatch =
                await householdService.CreateContributionRuleAsync(
                    new CreateHouseholdContributionRuleRequest(
                        SystemRoles.HouseholdMemberId,
                        HouseholdContributionModes.PercentageOfIncome,
                        null,
                        30m,
                        7,
                        householdAccountId,
                        new DateTime(
                            today.Year,
                            today.Month,
                            1,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null,
                        3),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var contributionRuleId =
                contributionBatch.RuleIds.Single();

            var periodKey =
                today.ToString(
                    "yyyy-MM",
                    System.Globalization.CultureInfo.InvariantCulture);

            var beforeEdit =
                await householdService.GetContributionOverviewAsync(
                    memberUserId);

            var originalObligation =
                beforeEdit!.Obligations
                    .Single(x =>
                        x.ContributionRuleId ==
                            contributionRuleId &&
                        x.PeriodKey ==
                            periodKey);

            Assert.Equal(
                900m,
                originalObligation.Amount);

            var originalObligationId =
                originalObligation.ObligationId;

            // To wcześniej kończyło się:
            // SQLite Error 19: FOREIGN KEY constraint failed,
            // bo zobowiązanie składki wskazywało na usuwane
            // PersonalRecurringOccurrence.
            await personalService.UpdateOwnRecurringRuleAsync(
                new UpdatePersonalRecurringRuleRequest(
                    salaryRuleId,
                    privateAccountId,
                    PersonalTransactionKinds.Income,
                    "wynagrodzenie",
                    4000m,
                    PersonalRecurringFrequencies.Monthly,
                    PersonalFinanceCategories.Salary,
                    "Pracodawca",
                    today,
                    null),
                memberUserId,
                Guid.NewGuid().ToString("N"));

            var afterEdit =
                await householdService.GetContributionOverviewAsync(
                    memberUserId);

            var rebuiltObligation =
                afterEdit!.Obligations
                    .Single(x =>
                        x.ContributionRuleId ==
                            contributionRuleId &&
                        x.PeriodKey ==
                            periodKey);

            Assert.Equal(
                1200m,
                rebuiltObligation.Amount);

            Assert.Equal(
                4000m,
                rebuiltObligation.PlannedIncomeAmount);

            Assert.NotEqual(
                originalObligationId,
                rebuiltObligation.ObligationId);

            Assert.Equal(
                1,
                await dbContext.HouseholdContributionObligations
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.ContributionRuleId ==
                            contributionRuleId &&
                        x.PeriodKey ==
                            periodKey));

            var currentSalaryOccurrence =
                (await personalService.GetOwnOverviewAsync(
                    memberUserId))
                    .RecurringOccurrences
                    .Single(x =>
                        x.RuleId ==
                            salaryRuleId &&
                        x.PeriodKey ==
                            periodKey);

            Assert.Equal(
                4000m,
                currentSalaryOccurrence.PlannedAmount);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(
                    root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
