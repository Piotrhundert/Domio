using Domio.Application.Authentication;
using Domio.Application.PersonalFinance;
using Domio.Domain.PersonalFinance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M03_4_RecurringManageConfirmTests
{
    [Fact]
    public async Task Overdue_salary_should_be_confirmable_and_should_use_actual_amount_once()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-4-confirm-{Guid.NewGuid():N}");

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

            Assert.True(schemaVersion >= 10);
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
                        500m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var plannedDate =
                DateTime.UtcNow.Date.AddDays(-9);

            await service.CreateOwnRecurringRuleAsync(
                new CreatePersonalRecurringRuleRequest(
                    accountId,
                    PersonalTransactionKinds.Income,
                    "Wynagrodzenie",
                    5800m,
                    PersonalRecurringFrequencies.Monthly,
                    PersonalFinanceCategories.Salary,
                    "Firma ABC",
                    plannedDate,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var before =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                500m,
                before.Accounts.Single().Balance);

            var overdue =
                before.RecurringOccurrences
                    .Where(x =>
                        x.StatusCode ==
                            PersonalRecurringOccurrenceStatuses.Planned)
                    .OrderBy(x => x.PlannedDateUtc)
                    .First();

            Assert.Equal(
                5800m,
                overdue.PlannedAmount);

            var actualDate =
                overdue.PlannedDateUtc.AddDays(2);

            await service.ConfirmOwnRecurringOccurrenceAsync(
                new ConfirmPersonalRecurringOccurrenceRequest(
                    overdue.OccurrenceId,
                    5900m,
                    actualDate,
                    "Wynagrodzenie rzeczywiste"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var after =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                6400m,
                after.Accounts.Single().Balance);

            var confirmed =
                after.RecurringOccurrences
                    .Single(x =>
                        x.OccurrenceId ==
                            overdue.OccurrenceId);

            Assert.Equal(
                PersonalRecurringOccurrenceStatuses.Confirmed,
                confirmed.StatusCode);

            Assert.Equal(
                5800m,
                confirmed.PlannedAmount);

            Assert.Equal(
                5900m,
                confirmed.ActualAmount);

            Assert.Equal(
                actualDate,
                confirmed.ActualDateUtc);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ConfirmOwnRecurringOccurrenceAsync(
                    new ConfirmPersonalRecurringOccurrenceRequest(
                        overdue.OccurrenceId,
                        5900m,
                        actualDate,
                        "Druga próba"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            Assert.Equal(
                6400m,
                (await service.GetOwnOverviewAsync(
                    administrator.UserId))
                .Accounts.Single().Balance);
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
    public async Task Editing_rule_should_keep_past_plan_and_deactivation_should_remove_only_future_plan()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-4-edit-{Guid.NewGuid():N}");

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

            var service =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var oldAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Stare konto",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        1000m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var newAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Nowe konto",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var startDate =
                DateTime.UtcNow.Date.AddDays(-5);

            var ruleId =
                await service.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        oldAccountId,
                        PersonalTransactionKinds.Income,
                        "Stara pensja",
                        100m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Stary pracodawca",
                        startDate,
                        null),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var beforeEdit =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            var pastOccurrence =
                beforeEdit.RecurringOccurrences
                    .Where(x =>
                        x.RuleId == ruleId &&
                        x.PlannedDateUtc <
                            DateTime.UtcNow.Date)
                    .OrderBy(x => x.PlannedDateUtc)
                    .First();

            Assert.Equal(
                100m,
                pastOccurrence.PlannedAmount);
            Assert.Equal(
                oldAccountId,
                pastOccurrence.AccountId);
            Assert.Equal(
                "Stara pensja",
                pastOccurrence.RuleName);

            await service.UpdateOwnRecurringRuleAsync(
                new UpdatePersonalRecurringRuleRequest(
                    ruleId,
                    newAccountId,
                    PersonalTransactionKinds.Income,
                    "Nowa pensja",
                    200m,
                    PersonalRecurringFrequencies.Monthly,
                    PersonalFinanceCategories.Salary,
                    "Nowy pracodawca",
                    startDate,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var afterEdit =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            var preservedPast =
                afterEdit.RecurringOccurrences
                    .Single(x =>
                        x.OccurrenceId ==
                            pastOccurrence.OccurrenceId);

            Assert.Equal(
                100m,
                preservedPast.PlannedAmount);
            Assert.Equal(
                oldAccountId,
                preservedPast.AccountId);
            Assert.Equal(
                "Stara pensja",
                preservedPast.RuleName);

            var future =
                afterEdit.RecurringOccurrences
                    .Where(x =>
                        x.RuleId == ruleId &&
                        x.StatusCode ==
                            PersonalRecurringOccurrenceStatuses.Planned &&
                        x.PlannedDateUtc >=
                            DateTime.UtcNow.Date)
                    .OrderBy(x => x.PlannedDateUtc)
                    .First();

            Assert.Equal(
                200m,
                future.PlannedAmount);
            Assert.Equal(
                newAccountId,
                future.AccountId);
            Assert.Equal(
                "Nowa pensja",
                future.RuleName);

            await service.DeactivateOwnRecurringRuleAsync(
                ruleId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var afterDelete =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Contains(
                afterDelete.RecurringRules,
                x =>
                    x.RuleId == ruleId &&
                    !x.IsActive);

            Assert.DoesNotContain(
                afterDelete.RecurringOccurrences,
                x =>
                    x.RuleId == ruleId &&
                    x.StatusCode ==
                        PersonalRecurringOccurrenceStatuses.Planned &&
                    x.PlannedDateUtc >=
                        DateTime.UtcNow.Date);

            Assert.Contains(
                afterDelete.RecurringOccurrences,
                x =>
                    x.OccurrenceId ==
                        pastOccurrence.OccurrenceId);
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
