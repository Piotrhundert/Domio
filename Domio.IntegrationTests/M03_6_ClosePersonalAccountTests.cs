using Domio.Application.Authentication;
using Domio.Application.PersonalFinance;
using Domio.Domain.PersonalFinance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M03_6_ClosePersonalAccountTests
{
    [Fact]
    public async Task Zero_balance_account_should_close_and_keep_financial_history()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-6-close-{Guid.NewGuid():N}");

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

            var sourceAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto do zamknięcia",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        100m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var targetAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto docelowe",
                        PersonalAccountTypes.SavingsAccount,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            await service.TransferBetweenOwnAccountsAsync(
                new CreatePersonalTransferRequest(
                    sourceAccountId,
                    targetAccountId,
                    100m,
                    DateTime.UtcNow,
                    "Przeniesienie środków przed zamknięciem"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var transactionCountBefore =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.AccountId == sourceAccountId);

            var info =
                await service.GetOwnAccountClosureInfoAsync(
                    sourceAccountId,
                    administrator.UserId);

            Assert.NotNull(info);
            Assert.True(info!.CanClose);
            Assert.Equal(0m, info.Balance);

            await service.CloseOwnAccountAsync(
                sourceAccountId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var closedAccount =
                await dbContext.PersonalFinancialAccounts
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id == sourceAccountId);

            Assert.False(closedAccount.IsActive);
            Assert.NotNull(closedAccount.ArchivedAtUtc);

            var transactionCountAfter =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.AccountId == sourceAccountId);

            Assert.Equal(
                transactionCountBefore,
                transactionCountAfter);

            var overview =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            var closedInOverview =
                overview.Accounts.Single(x =>
                    x.AccountId == sourceAccountId);

            Assert.False(closedInOverview.IsActive);
            Assert.Equal(0m, closedInOverview.Balance);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.PostOwnOperationAsync(
                    new PostPersonalOperationRequest(
                        sourceAccountId,
                        PersonalTransactionKinds.Income,
                        10m,
                        DateTime.UtcNow,
                        "Nie powinno się zapisać"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            var audit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.EventType ==
                        "M03.6.PersonalAccountClosed");

            var auditText =
                string.Join(
                    " ",
                    audit.Description,
                    audit.OldValuesJson,
                    audit.NewValuesJson);

            Assert.DoesNotContain(
                "Konto do zamknięcia",
                auditText);

            Assert.DoesNotContain(
                "100",
                auditText);
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
    public async Task Account_with_nonzero_balance_should_not_close()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-6-balance-{Guid.NewGuid():N}");

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

            var accountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto aktywne",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        50m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var error =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CloseOwnAccountAsync(
                        accountId,
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "niezerowym saldem",
                error.Message);

            var account =
                await dbContext.PersonalFinancialAccounts
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id == accountId);

            Assert.True(account.IsActive);
            Assert.Null(account.ArchivedAtUtc);
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
    public async Task Account_used_by_recurring_rule_or_unconfirmed_plan_should_not_close()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-6-recurring-{Guid.NewGuid():N}");

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

            var accountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto wynagrodzenia",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        0m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var ruleId =
                await service.CreateOwnRecurringRuleAsync(
                    new CreatePersonalRecurringRuleRequest(
                        accountId,
                        PersonalTransactionKinds.Income,
                        "Wynagrodzenie",
                        5000m,
                        PersonalRecurringFrequencies.Monthly,
                        PersonalFinanceCategories.Salary,
                        "Firma Test",
                        DateTime.UtcNow.Date.AddDays(-5),
                        null),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var activeRuleError =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CloseOwnAccountAsync(
                        accountId,
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "aktywną operację cykliczną",
                activeRuleError.Message);

            await service.DeactivateOwnRecurringRuleAsync(
                ruleId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var info =
                await service.GetOwnAccountClosureInfoAsync(
                    accountId,
                    administrator.UserId);

            Assert.NotNull(info);
            Assert.Equal(
                0,
                info!.ActiveRecurringRules);

            Assert.True(
                info.PlannedRecurringOccurrences > 0);

            var plannedError =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CloseOwnAccountAsync(
                        accountId,
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "niepotwierdzone planowane operacje",
                plannedError.Message);
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
