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

public sealed class M03_9_ModuleRegressionGateTests
{
    [Fact]
    public async Task G03_full_personal_finance_flow_should_remain_consistent()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m03-9-g03-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);

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
                new DomioDbContext(options);

            await dbContext.Database.MigrateAsync();

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.Equal(
                11,
                schemaVersion);

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

            var otherUserId =
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

            var guestUserId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Gość",
                        "Testowy",
                        null,
                        null,
                        "guest@example.test",
                        "DomioGuest123",
                        SystemRoles.GuestId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var service =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var bankAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto główne",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        1000m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var cashAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Gotówka",
                        PersonalAccountTypes.Cash,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var otherUserAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto Anny",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        100m),
                    otherUserId,
                    Guid.NewGuid().ToString("N"));

            await service.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    bankAccountId,
                    PersonalTransactionKinds.Income,
                    500m,
                    DateTime.UtcNow.Date.AddHours(12),
                    "Dodatkowy wpływ",
                    PersonalFinanceCategories.OtherIncome,
                    "Źródło testowe"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var expenseId =
                await service.PostOwnOperationAsync(
                    new PostPersonalOperationRequest(
                        bankAccountId,
                        PersonalTransactionKinds.Expense,
                        120m,
                        DateTime.UtcNow.Date.AddHours(12),
                        "Zakupy",
                        PersonalFinanceCategories.Food,
                        "Sklep Test"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            await service.TransferBetweenOwnAccountsAsync(
                new CreatePersonalTransferRequest(
                    bankAccountId,
                    cashAccountId,
                    300m,
                    DateTime.UtcNow.Date.AddHours(12),
                    "Wypłata gotówki"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await service.CorrectOwnTransactionAsync(
                new CorrectPersonalTransactionRequest(
                    expenseId,
                    100m,
                    PersonalFinanceCategories.Food),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var recurringStart =
                new DateTime(
                    DateTime.UtcNow.Year,
                    DateTime.UtcNow.Month,
                    Math.Min(
                        10,
                        DateTime.DaysInMonth(
                            DateTime.UtcNow.Year,
                            DateTime.UtcNow.Month)),
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            var salaryRuleId =
                await service.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        bankAccountId,
                        PersonalTransactionKinds.Income,
                        "Wynagrodzenie",
                        5800m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Firma ABC",
                        recurringStart,
                        null),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var subscriptionRuleId =
                await service.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        bankAccountId,
                        PersonalTransactionKinds.Expense,
                        "Streaming",
                        43m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Subscription,
                        "Streaming Test",
                        recurringStart.AddDays(2),
                        null),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var occurrenceCountBefore =
                await dbContext.PersonalRecurringOccurrences
                    .CountAsync();

            _ = await service.GetOwnOverviewAsync(
                administrator.UserId);

            _ = await service.GetOwnOverviewAsync(
                administrator.UserId);

            var occurrenceCountAfter =
                await dbContext.PersonalRecurringOccurrences
                    .CountAsync();

            Assert.Equal(
                occurrenceCountBefore,
                occurrenceCountAfter);

            var overviewBeforeConfirm =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                1100m,
                overviewBeforeConfirm.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            Assert.Equal(
                300m,
                overviewBeforeConfirm.Accounts
                    .Single(x =>
                        x.AccountId ==
                            cashAccountId)
                    .Balance);

            var salaryOccurrence =
                overviewBeforeConfirm
                    .RecurringOccurrences
                    .Where(x =>
                        x.RuleId ==
                            salaryRuleId &&
                        x.StatusCode ==
                            PersonalRecurringOccurrenceStatuses.Planned)
                    .OrderBy(x =>
                        x.PlannedDateUtc)
                    .First();

            await service.ConfirmOwnRecurringOccurrenceAsync(
                new ConfirmPersonalRecurringOccurrenceRequest(
                    salaryOccurrence.OccurrenceId,
                    6100m,
                    salaryOccurrence.PlannedDateUtc.AddDays(1),
                    "Wynagrodzenie rzeczywiste"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var subscriptionOccurrence =
                (await service.GetOwnOverviewAsync(
                    administrator.UserId))
                    .RecurringOccurrences
                    .Where(x =>
                        x.RuleId ==
                            subscriptionRuleId &&
                        x.StatusCode ==
                            PersonalRecurringOccurrenceStatuses.Planned)
                    .OrderBy(x =>
                        x.PlannedDateUtc)
                    .First();

            await service.ConfirmOwnRecurringOccurrenceAsync(
                new ConfirmPersonalRecurringOccurrenceRequest(
                    subscriptionOccurrence.OccurrenceId,
                    43m,
                    subscriptionOccurrence.PlannedDateUtc,
                    "Streaming"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var afterRecurring =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                7157m,
                afterRecurring.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            var salaryRule =
                afterRecurring.RecurringRules
                    .Single(x =>
                        x.RuleId ==
                            salaryRuleId);

            Assert.Equal(
                5800m,
                salaryRule.PlannedAmount);

            var confirmedSalary =
                afterRecurring.RecurringOccurrences
                    .Single(x =>
                        x.OccurrenceId ==
                            salaryOccurrence.OccurrenceId);

            Assert.Equal(
                5800m,
                confirmedSalary.PlannedAmount);

            Assert.Equal(
                6100m,
                confirmedSalary.ActualAmount);

            var foodHistory =
                await service.GetOwnTransactionHistoryAsync(
                    new PersonalTransactionHistoryFilter(
                        null,
                        null,
                        bankAccountId,
                        null,
                        PersonalFinanceCategories.Food),
                    administrator.UserId);

            Assert.Equal(
                2,
                foodHistory.TotalCount);

            Assert.Contains(
                foodHistory.Items,
                x =>
                    x.TransactionId ==
                    expenseId &&
                    x.Amount ==
                    -120m);

            Assert.Contains(
                foodHistory.Items,
                x =>
                    x.CorrectsTransactionId ==
                    expenseId &&
                    x.Amount ==
                    20m);

            await service.TransferBetweenOwnAccountsAsync(
                new CreatePersonalTransferRequest(
                    cashAccountId,
                    bankAccountId,
                    300m,
                    DateTime.UtcNow.Date.AddHours(12),
                    "Zwrot gotówki"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var closeInfo =
                await service.GetOwnAccountClosureInfoAsync(
                    cashAccountId,
                    administrator.UserId);

            Assert.NotNull(closeInfo);
            Assert.True(
                closeInfo!.CanClose);

            await service.CloseOwnAccountAsync(
                cashAccountId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var finalOverview =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                7457m,
                finalOverview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            Assert.False(
                finalOverview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            cashAccountId)
                    .IsActive);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () =>
                    service.PostOwnOperationAsync(
                        new PostPersonalOperationRequest(
                            otherUserAccountId,
                            PersonalTransactionKinds.Expense,
                            1m,
                            DateTime.UtcNow,
                            "Próba cudzego konta"),
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            var administratorHistory =
                await service.GetOwnTransactionHistoryAsync(
                    new PersonalTransactionHistoryFilter(
                        null,
                        null,
                        null,
                        null,
                        null),
                    administrator.UserId);

            Assert.DoesNotContain(
                administratorHistory.Items,
                x =>
                    x.AccountId ==
                    otherUserAccountId);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () =>
                    service.CreateOwnAccountAsync(
                        new CreatePersonalAccountRequest(
                            "Konto gościa",
                            PersonalAccountTypes.Cash,
                            "PLN",
                            10m),
                        guestUserId,
                        Guid.NewGuid().ToString("N")));

            var balanceBeforeRejected =
                finalOverview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance;

            var insufficient =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.PostOwnOperationAsync(
                            new PostPersonalOperationRequest(
                                bankAccountId,
                                PersonalTransactionKinds.Expense,
                                balanceBeforeRejected + 1m,
                                DateTime.UtcNow,
                                "Za duży wydatek",
                                PersonalFinanceCategories.OtherExpense),
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "Niewystarczające środki",
                insufficient.Message);

            Assert.Equal(
                balanceBeforeRejected,
                (await service.GetOwnOverviewAsync(
                    administrator.UserId))
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            var auditRows =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.EventType.StartsWith("M03."))
                    .ToListAsync();

            Assert.NotEmpty(
                auditRows);

            Assert.All(
                auditRows,
                entry =>
                {
                    var auditText =
                        string.Join(
                            " ",
                            entry.Description,
                            entry.OldValuesJson,
                            entry.NewValuesJson);

                    Assert.DoesNotContain(
                        "6100",
                        auditText);

                    Assert.DoesNotContain(
                        "Sklep Test",
                        auditText);
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

    [Fact]
    public void G03_money_precision_should_stay_exact()
    {
        Assert.Throws<ArgumentException>(
            () =>
                PersonalFinanceMoney.ToMinorUnits(
                    12.345m));

        Assert.Equal(
            1234,
            PersonalFinanceMoney.ToMinorUnits(
                12.34m));

        Assert.Equal(
            12.34m,
            PersonalFinanceMoney.FromMinorUnits(
                1234));
    }
}
