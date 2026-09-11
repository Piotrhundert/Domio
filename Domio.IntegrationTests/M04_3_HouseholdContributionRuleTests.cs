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

public sealed class M04_3_HouseholdContributionRuleTests
{
    [Fact]
    public async Task Role_based_percentage_contribution_should_create_rule_for_every_user_in_role()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-3-role-{Guid.NewGuid():N}");

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
                schemaVersion >= 13);

            Assert.Empty(
                await dbContext.Database
                    .GetPendingMigrationsAsync());

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

            var annaUserId =
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

            var piotrUserId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Piotr",
                        "Domownik",
                        null,
                        null,
                        "piotr@example.test",
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
                        "Konto Dom 1",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var personalService =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var annaAccountId =
                await personalService.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto Anny",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        0m),
                    annaUserId,
                    Guid.NewGuid().ToString("N"));

            var piotrAccountId =
                await personalService.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto Piotra",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        0m),
                    piotrUserId,
                    Guid.NewGuid().ToString("N"));

            var now =
                DateTime.UtcNow;

            var annaSalaryRuleId =
                await personalService.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        annaAccountId,
                        PersonalTransactionKinds.Income,
                        "Wynagrodzenie Anny",
                        5800m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Firma A",
                        new DateTime(
                            now.Year,
                            now.Month,
                            10,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null),
                    annaUserId,
                    Guid.NewGuid().ToString("N"));

            var piotrSalaryRuleId =
                await personalService.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        piotrAccountId,
                        PersonalTransactionKinds.Income,
                        "Wynagrodzenie Piotra",
                        4000m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Firma P",
                        new DateTime(
                            now.Year,
                            now.Month,
                            15,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null),
                    piotrUserId,
                    Guid.NewGuid().ToString("N"));

            var annaSalaryOccurrence =
                (await personalService.GetOwnOverviewAsync(
                    annaUserId))
                    .RecurringOccurrences
                    .Where(x =>
                        x.RuleId ==
                            annaSalaryRuleId &&
                        x.PlannedDateUtc.Year ==
                            now.Year &&
                        x.PlannedDateUtc.Month ==
                            now.Month)
                    .Single();

            await personalService.ConfirmOwnRecurringOccurrenceAsync(
                new ConfirmPersonalRecurringOccurrenceRequest(
                    annaSalaryOccurrence.OccurrenceId,
                    6100m,
                    annaSalaryOccurrence.PlannedDateUtc.AddDays(2),
                    "Rzeczywiste wynagrodzenie"),
                annaUserId,
                Guid.NewGuid().ToString("N"));

            var batch =
                await householdService.CreateContributionRuleAsync(
                    new CreateHouseholdContributionRuleRequest(
                        SystemRoles.HouseholdMemberId,
                        HouseholdContributionModes.PercentageOfIncome,
                        null,
                        30m,
                        7,
                        householdAccountId,
                        new DateTime(
                            now.Year,
                            now.Month,
                            1,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null,
                        3),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(
                SystemRoles.HouseholdMemberId,
                batch.RoleDefinitionId);

            Assert.Equal(
                2,
                batch.UsersMatched);

            Assert.Equal(
                2,
                batch.RulesCreated);

            Assert.Equal(
                2,
                batch.RuleIds.Count);

            var contributionRules =
                await dbContext.HouseholdContributionRules
                    .AsNoTracking()
                    .Where(x =>
                        batch.RuleIds.Contains(
                            x.Id))
                    .ToArrayAsync();

            Assert.Equal(
                2,
                contributionRules.Length);

            var memberships =
                await dbContext.HouseholdMembers
                    .AsNoTracking()
                    .ToArrayAsync();

            Assert.Equal(
                3,
                memberships.Length);

            var annaPersonId =
                await dbContext.UserAccounts
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                            annaUserId)
                    .Select(x =>
                        x.PersonId)
                    .SingleAsync();

            var piotrPersonId =
                await dbContext.UserAccounts
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                            piotrUserId)
                    .Select(x =>
                        x.PersonId)
                    .SingleAsync();

            var annaMembership =
                memberships.Single(x =>
                    x.PersonId ==
                        annaPersonId);

            var piotrMembership =
                memberships.Single(x =>
                    x.PersonId ==
                        piotrPersonId);

            Assert.Contains(
                contributionRules,
                x =>
                    x.HouseholdMemberId ==
                        annaMembership.Id &&
                    x.IncomeRuleId ==
                        annaSalaryRuleId);

            Assert.Contains(
                contributionRules,
                x =>
                    x.HouseholdMemberId ==
                        piotrMembership.Id &&
                    x.IncomeRuleId ==
                        piotrSalaryRuleId);

            var overview =
                await householdService.GetContributionOverviewAsync(
                    administrator.UserId);

            Assert.NotNull(
                overview);

            var roleOption =
                overview!.Roles.Single(x =>
                    x.RoleDefinitionId ==
                        SystemRoles.HouseholdMemberId);

            Assert.Equal(
                2,
                roleOption.UserCount);

            var annaObligation =
                overview.Obligations
                    .Where(x =>
                        x.HouseholdMemberId ==
                            annaMembership.Id &&
                        x.PeriodKey ==
                            annaSalaryOccurrence.PeriodKey)
                    .Single();

            Assert.Equal(
                1740m,
                annaObligation.Amount);

            Assert.Equal(
                5800m,
                annaObligation.PlannedIncomeAmount);

            Assert.Equal(
                annaSalaryOccurrence.PlannedDateUtc.Date.AddDays(7),
                annaObligation.DueDateUtc.Date);

            var piotrOccurrence =
                (await personalService.GetOwnOverviewAsync(
                    piotrUserId))
                    .RecurringOccurrences
                    .Where(x =>
                        x.RuleId ==
                            piotrSalaryRuleId &&
                        x.PlannedDateUtc.Year ==
                            now.Year &&
                        x.PlannedDateUtc.Month ==
                            now.Month)
                    .Single();

            var piotrObligation =
                overview.Obligations
                    .Where(x =>
                        x.HouseholdMemberId ==
                            piotrMembership.Id &&
                        x.PeriodKey ==
                            piotrOccurrence.PeriodKey)
                    .Single();

            Assert.Equal(
                1200m,
                piotrObligation.Amount);

            Assert.Equal(
                4000m,
                piotrObligation.PlannedIncomeAmount);

            Assert.Equal(
                piotrOccurrence.PlannedDateUtc.Date.AddDays(7),
                piotrObligation.DueDateUtc.Date);

            var obligationCountBefore =
                await dbContext.HouseholdContributionObligations
                    .CountAsync();

            _ = await householdService.GetContributionOverviewAsync(
                administrator.UserId);

            _ = await householdService.GetContributionOverviewAsync(
                administrator.UserId);

            var obligationCountAfter =
                await dbContext.HouseholdContributionObligations
                    .CountAsync();

            Assert.Equal(
                obligationCountBefore,
                obligationCountAfter);

            var confirmedAnnaSalary =
                (await personalService.GetOwnOverviewAsync(
                    annaUserId))
                    .RecurringOccurrences
                    .Single(x =>
                        x.OccurrenceId ==
                            annaSalaryOccurrence.OccurrenceId);

            Assert.Equal(
                5800m,
                confirmedAnnaSalary.PlannedAmount);

            Assert.Equal(
                6100m,
                confirmedAnnaSalary.ActualAmount);

            var audit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.EventType ==
                            "M04.3.HouseholdContributionRoleBatchCreated");

            var auditText =
                string.Join(
                    " ",
                    audit.Description,
                    audit.OldValuesJson,
                    audit.NewValuesJson);

            Assert.DoesNotContain(
                "5800",
                auditText);

            Assert.DoesNotContain(
                "6100",
                auditText);

            Assert.DoesNotContain(
                "1740",
                auditText);
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

    [Fact]
    public async Task Role_based_rule_should_wait_for_salary_and_generate_automatically_after_user_adds_it()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-3-deferred-salary-{Guid.NewGuid():N}");

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

            var noSalaryUserId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Bez",
                        "Pensji",
                        null,
                        null,
                        "nopay@example.test",
                        "DomioNoPay123",
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
                        "Konto Dom 1",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var now =
                DateTime.UtcNow;

            var batch =
                await householdService.CreateContributionRuleAsync(
                    new CreateHouseholdContributionRuleRequest(
                        SystemRoles.HouseholdMemberId,
                        HouseholdContributionModes.PercentageOfIncome,
                        null,
                        30m,
                        7,
                        householdAccountId,
                        new DateTime(
                            now.Year,
                            now.Month,
                            20,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null,
                        3),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(
                1,
                batch.RulesCreated);

            Assert.Equal(
                1,
                batch.RulesWaitingForIncomePlan);

            var pendingRule =
                await dbContext.HouseholdContributionRules
                    .SingleAsync();

            Assert.Null(
                pendingRule.IncomeRuleId);

            Assert.Equal(
                0,
                await dbContext.HouseholdContributionObligations
                    .CountAsync());

            var personalService =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var privateAccountId =
                await personalService.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto prywatne",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        0m),
                    noSalaryUserId,
                    Guid.NewGuid().ToString("N"));

            var salaryRuleId =
                await personalService.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        privateAccountId,
                        PersonalTransactionKinds.Income,
                        "Wynagrodzenie",
                        5000m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Firma Test",
                        new DateTime(
                            now.Year,
                            now.Month,
                            10,
                            0,
                            0,
                            0,
                            DateTimeKind.Utc),
                        null),
                    noSalaryUserId,
                    Guid.NewGuid().ToString("N"));

            // Użytkownik nie musi wracać do konfiguracji składki.
            // Samo otwarcie widoku składek uruchamia automatyczny
            // resolver i generator.
            var memberOverview =
                await householdService.GetContributionOverviewAsync(
                    noSalaryUserId);

            var resolvedMemberOverview =
                memberOverview
                ?? throw new InvalidOperationException(
                    "Oczekiwano widoku składek domownika.");

            var linkedRule =
                await dbContext.HouseholdContributionRules
                    .AsNoTracking()
                    .SingleAsync();

            Assert.Equal(
                salaryRuleId,
                linkedRule.IncomeRuleId);

            var salaryOccurrence =
                (await personalService.GetOwnOverviewAsync(
                    noSalaryUserId))
                    .RecurringOccurrences
                    .Where(x =>
                        x.RuleId ==
                            salaryRuleId &&
                        x.PlannedDateUtc.Year ==
                            now.Year &&
                        x.PlannedDateUtc.Month ==
                            now.Month)
                    .Single();

            var obligation =
                resolvedMemberOverview.Obligations
                    .Single(x =>
                        x.PeriodKey ==
                            salaryOccurrence.PeriodKey);

            Assert.Equal(
                1500m,
                obligation.Amount);

            Assert.Equal(
                5000m,
                obligation.PlannedIncomeAmount);

            Assert.Equal(
                salaryOccurrence.PlannedDateUtc.Date.AddDays(7),
                obligation.DueDateUtc.Date);

            var countBefore =
                await dbContext.HouseholdContributionObligations
                    .CountAsync();

            _ = await householdService.GetContributionOverviewAsync(
                noSalaryUserId);

            _ = await householdService.GetContributionOverviewAsync(
                administrator.UserId);

            Assert.Equal(
                countBefore,
                await dbContext.HouseholdContributionObligations
                    .CountAsync());
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
