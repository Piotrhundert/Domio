using Domio.Application.Authentication;
using Domio.Application.PersonalFinance;
using Domio.Domain.PersonalFinance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M03_7_PersonalTransactionCorrectionTests
{
    [Fact]
    public async Task Expense_correction_should_preserve_source_and_fix_balance()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-7-correction-{Guid.NewGuid():N}");

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

            Assert.True(schemaVersion >= 11);
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

            var sourceTransactionId =
                await service.PostOwnOperationAsync(
                    new PostPersonalOperationRequest(
                        accountId,
                        PersonalTransactionKinds.Expense,
                        120m,
                        DateTime.UtcNow,
                        "Zakupy",
                        PersonalFinanceCategories.Food,
                        "Sklep Test"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var before =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                380m,
                before.Accounts.Single().Balance);

            var sourceBefore =
                before.RecentTransactions
                    .Single(x =>
                        x.TransactionId ==
                            sourceTransactionId);

            Assert.Equal(
                PersonalFinanceCategories.Food,
                sourceBefore.CategoryCode);

            Assert.Equal(
                "Sklep Test",
                sourceBefore.Counterparty);

            var correctionId =
                await service.CorrectOwnTransactionAsync(
                    new CorrectPersonalTransactionRequest(
                        sourceTransactionId,
                        100m,
                        PersonalFinanceCategories.Home),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            Assert.NotNull(correctionId);

            var after =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Equal(
                400m,
                after.Accounts.Single().Balance);

            var source =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id == sourceTransactionId);

            Assert.Equal(
                -12000,
                source.AmountMinor);

            Assert.Equal(
                PersonalFinanceCategories.Home,
                source.CategoryCode);

            var correction =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.Id == correctionId);

            Assert.Equal(
                PersonalTransactionKinds.Correction,
                correction.KindCode);

            Assert.Equal(
                2000,
                correction.AmountMinor);

            Assert.Equal(
                sourceTransactionId,
                correction.CorrectsTransactionId);

            Assert.Equal(
                PersonalFinanceCategories.Home,
                correction.CategoryCode);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CorrectOwnTransactionAsync(
                    new CorrectPersonalTransactionRequest(
                        sourceTransactionId,
                        90m,
                        PersonalFinanceCategories.Home),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            Assert.Equal(
                400m,
                (await service.GetOwnOverviewAsync(
                    administrator.UserId))
                .Accounts.Single().Balance);

            var audit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .SingleAsync(x =>
                        x.EventType ==
                        "M03.7.PersonalTransactionCorrected");

            var auditText =
                string.Join(
                    " ",
                    audit.Description,
                    audit.OldValuesJson,
                    audit.NewValuesJson);

            Assert.DoesNotContain("120", auditText);
            Assert.DoesNotContain("100", auditText);
            Assert.DoesNotContain("Zakupy", auditText);
            Assert.DoesNotContain("Sklep Test", auditText);
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
    public async Task Correction_that_would_make_balance_negative_should_be_blocked()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-7-negative-{Guid.NewGuid():N}");

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
                        "Konto",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        200m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var sourceId =
                await service.PostOwnOperationAsync(
                    new PostPersonalOperationRequest(
                        accountId,
                        PersonalTransactionKinds.Expense,
                        100m,
                        DateTime.UtcNow,
                        "Wydatek",
                        PersonalFinanceCategories.OtherExpense),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(
                100m,
                (await service.GetOwnOverviewAsync(
                    administrator.UserId))
                .Accounts.Single().Balance);

            var error =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CorrectOwnTransactionAsync(
                        new CorrectPersonalTransactionRequest(
                            sourceId,
                            250m,
                            PersonalFinanceCategories.OtherExpense),
                        administrator.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "poniżej zera",
                error.Message);

            Assert.Equal(
                100m,
                (await service.GetOwnOverviewAsync(
                    administrator.UserId))
                .Accounts.Single().Balance);

            var corrections =
                await dbContext.PersonalFinancialTransactions
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.CorrectsTransactionId ==
                            sourceId);

            Assert.Equal(0, corrections);
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
