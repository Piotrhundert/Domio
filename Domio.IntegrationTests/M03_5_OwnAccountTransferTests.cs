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

public sealed class M03_5_OwnAccountTransferTests
{
    [Fact]
    public async Task Transfer_should_move_money_atomically_between_own_accounts()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m03-5-transfer-{Guid.NewGuid():N}");

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
                10,
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

            var service =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var sourceAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto główne",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        1000m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var targetAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Oszczędności",
                        PersonalAccountTypes.SavingsAccount,
                        "PLN",
                        100m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var transfer =
                await service.TransferBetweenOwnAccountsAsync(
                    new CreatePersonalTransferRequest(
                        sourceAccountId,
                        targetAccountId,
                        250m,
                        DateTime.UtcNow,
                        "Na oszczędności"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(
                sourceAccountId,
                transfer.SourceAccountId);

            Assert.Equal(
                targetAccountId,
                transfer.TargetAccountId);

            Assert.Equal(
                250m,
                transfer.Amount);

            Assert.Equal(
                "PLN",
                transfer.CurrencyCode);

            var overview =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            var sourceAccount =
                overview.Accounts.Single(x =>
                    x.AccountId ==
                    sourceAccountId);

            var targetAccount =
                overview.Accounts.Single(x =>
                    x.AccountId ==
                    targetAccountId);

            Assert.Equal(
                750m,
                sourceAccount.Balance);

            Assert.Equal(
                350m,
                targetAccount.Balance);

            Assert.Equal(
                1100m,
                overview.Accounts.Sum(x =>
                    x.Balance));

            var transferRows =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                            transfer.SourceTransactionId ||
                        x.Id ==
                            transfer.TargetTransactionId)
                    .ToListAsync();

            Assert.Equal(
                2,
                transferRows.Count);

            var transferOut =
                transferRows.Single(x =>
                    x.KindCode ==
                    PersonalTransactionKinds.TransferOut);

            var transferIn =
                transferRows.Single(x =>
                    x.KindCode ==
                    PersonalTransactionKinds.TransferIn);

            Assert.Equal(
                -25000,
                transferOut.AmountMinor);

            Assert.Equal(
                25000,
                transferIn.AmountMinor);

            Assert.Equal(
                transferOut.OccurredAtUtc,
                transferIn.OccurredAtUtc);

            var audit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.EventType ==
                        "M03.5.PersonalAccountTransferPosted");

            var auditText =
                string.Join(
                    " ",
                    audit.Description,
                    audit.OldValuesJson,
                    audit.NewValuesJson);

            Assert.DoesNotContain(
                "250",
                auditText);

            Assert.DoesNotContain(
                "Konto główne",
                auditText);

            Assert.DoesNotContain(
                "Oszczędności",
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
    public async Task Failed_transfer_should_not_change_either_account()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m03-5-failed-{Guid.NewGuid():N}");

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
                        "Źródło",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        100m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var targetAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Cel",
                        PersonalAccountTypes.SavingsAccount,
                        "PLN",
                        50m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var error =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.TransferBetweenOwnAccountsAsync(
                            new CreatePersonalTransferRequest(
                                sourceAccountId,
                                targetAccountId,
                                150m,
                                DateTime.UtcNow,
                                null),
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "Niewystarczające środki",
                error.Message);

            var overview =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                100m,
                overview.Accounts
                    .Single(x =>
                        x.AccountId ==
                        sourceAccountId)
                    .Balance);

            Assert.Equal(
                50m,
                overview.Accounts
                    .Single(x =>
                        x.AccountId ==
                        targetAccountId)
                    .Balance);

            var transferRowCount =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.KindCode ==
                            PersonalTransactionKinds.TransferOut ||
                        x.KindCode ==
                            PersonalTransactionKinds.TransferIn);

            Assert.Equal(
                0,
                transferRowCount);
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
    public async Task Transfer_should_reject_different_currency_and_other_users_account()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m03-5-security-{Guid.NewGuid():N}");

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

            var service =
                new PersonalFinanceService(
                    dbContext,
                    auditService);

            var adminPlnAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Admin PLN",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        1000m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var adminEurAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Admin EUR",
                        PersonalAccountTypes.BankAccount,
                        "EUR",
                        100m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var annaAccountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Anna PLN",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        100m),
                    annaUserId,
                    Guid.NewGuid().ToString("N"));

            var currencyError =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.TransferBetweenOwnAccountsAsync(
                            new CreatePersonalTransferRequest(
                                adminPlnAccountId,
                                adminEurAccountId,
                                10m,
                                DateTime.UtcNow,
                                null),
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "różnych walutach",
                currencyError.Message);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () =>
                    service.TransferBetweenOwnAccountsAsync(
                        new CreatePersonalTransferRequest(
                            adminPlnAccountId,
                            annaAccountId,
                            10m,
                            DateTime.UtcNow,
                            null),
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            var adminOverview =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                1000m,
                adminOverview.Accounts
                    .Single(x =>
                        x.AccountId ==
                        adminPlnAccountId)
                    .Balance);

            Assert.Equal(
                100m,
                adminOverview.Accounts
                    .Single(x =>
                        x.AccountId ==
                        adminEurAccountId)
                    .Balance);

            var annaOverview =
                await service.GetOwnOverviewAsync(
                    annaUserId);

            Assert.Equal(
                100m,
                annaOverview.Accounts
                    .Single(x =>
                        x.AccountId ==
                        annaAccountId)
                    .Balance);
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
