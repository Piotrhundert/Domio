using Domio.Application.Authentication;
using Domio.Application.HouseholdFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_2_HouseholdTransferAndCloseTests
{
    [Fact]
    public async Task Transfer_should_be_atomic_and_closing_zero_balance_account_should_keep_history()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-2-transfer-{Guid.NewGuid():N}");

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
                schemaVersion >= 12);

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

            var service =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var bankAccountId =
                await service.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Konto bankowe domu",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        1000m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var cashAccountId =
                await service.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Gotówka domu",
                        HouseholdAccountTypes.Cash,
                        "PLN",
                        100m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var transfer =
                await service.TransferBetweenAccountsAsync(
                    new CreateHouseholdTransferRequest(
                        bankAccountId,
                        cashAccountId,
                        300m,
                        DateTime.UtcNow,
                        "Wypłata gotówki"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var overview =
                await service.GetOverviewAsync(
                    administrator.UserId);

            Assert.NotNull(
                overview);

            Assert.Equal(
                700m,
                overview!.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            Assert.Equal(
                400m,
                overview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            cashAccountId)
                    .Balance);

            Assert.Equal(
                1100m,
                overview.Accounts.Sum(x =>
                    x.Balance));

            var transferEntries =
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .Where(x =>
                        x.SourceType ==
                            "HouseholdTransfer" &&
                        x.SourceId ==
                            transfer.TransferId.ToString())
                    .ToArrayAsync();

            Assert.Equal(
                2,
                transferEntries.Length);

            Assert.Contains(
                transferEntries,
                x =>
                    x.AccountId ==
                        bankAccountId &&
                    x.EntryTypeCode ==
                        HouseholdEntryTypes.TransferOut &&
                    x.AmountMinor ==
                        -30000);

            Assert.Contains(
                transferEntries,
                x =>
                    x.AccountId ==
                        cashAccountId &&
                    x.EntryTypeCode ==
                        HouseholdEntryTypes.TransferIn &&
                    x.AmountMinor ==
                        30000);

            Assert.Equal(
                transferEntries[0].OccurredAtUtc,
                transferEntries[1].OccurredAtUtc);

            var transferEntryCountBeforeRejected =
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.SourceType ==
                            "HouseholdTransfer");

            var insufficient =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.TransferBetweenAccountsAsync(
                            new CreateHouseholdTransferRequest(
                                bankAccountId,
                                cashAccountId,
                                800m,
                                DateTime.UtcNow,
                                "Za duży transfer"),
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "Niewystarczające środki",
                insufficient.Message);

            Assert.Equal(
                transferEntryCountBeforeRejected,
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.SourceType ==
                            "HouseholdTransfer"));

            var afterRejected =
                await service.GetOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                700m,
                afterRejected!.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            Assert.Equal(
                400m,
                afterRejected.Accounts
                    .Single(x =>
                        x.AccountId ==
                            cashAccountId)
                    .Balance);

            await service.TransferBetweenAccountsAsync(
                new CreateHouseholdTransferRequest(
                    cashAccountId,
                    bankAccountId,
                    400m,
                    DateTime.UtcNow,
                    "Wpłata gotówki do banku"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var closureInfo =
                await service.GetAccountClosureInfoAsync(
                    cashAccountId,
                    administrator.UserId);

            Assert.NotNull(
                closureInfo);

            Assert.True(
                closureInfo!.CanClose);

            Assert.Equal(
                0m,
                closureInfo.Balance);

            var historicalEntryCount =
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.AccountId ==
                            cashAccountId);

            await service.CloseAccountAsync(
                cashAccountId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var closed =
                await dbContext.HouseholdAccounts
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id ==
                            cashAccountId);

            Assert.False(
                closed.IsActive);

            Assert.NotNull(
                closed.ArchivedAtUtc);

            Assert.Equal(
                historicalEntryCount,
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.AccountId ==
                            cashAccountId));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () =>
                    service.PostOperationAsync(
                        new PostHouseholdOperationRequest(
                            cashAccountId,
                            HouseholdEntryTypes.Income,
                            1m,
                            DateTime.UtcNow,
                            HouseholdFinanceCategories.HouseholdIncome,
                            "Nie powinno się zapisać"),
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            var nonzeroClose =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.CloseAccountAsync(
                            bankAccountId,
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "niezerowym saldem",
                nonzeroClose.Message);

            var auditRows =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.EventType ==
                            "M04.2.HouseholdTransferPosted" ||
                        x.EventType ==
                            "M04.2.HouseholdAccountClosed")
                    .ToArrayAsync();

            Assert.Equal(
                3,
                auditRows.Length);

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
                        "300",
                        auditText);

                    Assert.DoesNotContain(
                        "Gotówka domu",
                        auditText);

                    Assert.DoesNotContain(
                        "Konto bankowe domu",
                        auditText);
                });
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
    public async Task Transfer_should_reject_same_account_and_different_currency_without_partial_booking()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-2-validation-{Guid.NewGuid():N}");

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

            var service =
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var plnAccountId =
                await service.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "PLN",
                        HouseholdAccountTypes.Bank,
                        "PLN",
                        100m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var eurAccountId =
                await service.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "EUR",
                        HouseholdAccountTypes.Bank,
                        "EUR",
                        50m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            await Assert.ThrowsAsync<ArgumentException>(
                () =>
                    service.TransferBetweenAccountsAsync(
                        new CreateHouseholdTransferRequest(
                            plnAccountId,
                            plnAccountId,
                            10m,
                            DateTime.UtcNow,
                            null),
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            var currencyError =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.TransferBetweenAccountsAsync(
                            new CreateHouseholdTransferRequest(
                                plnAccountId,
                                eurAccountId,
                                10m,
                                DateTime.UtcNow,
                                null),
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "różnych walutach",
                currencyError.Message);

            Assert.Equal(
                0,
                await dbContext.HouseholdEntries
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.SourceType ==
                            "HouseholdTransfer"));

            var overview =
                await service.GetOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                100m,
                overview!.Accounts
                    .Single(x =>
                        x.AccountId ==
                            plnAccountId)
                    .Balance);

            Assert.Equal(
                50m,
                overview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            eurAccountId)
                    .Balance);
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
