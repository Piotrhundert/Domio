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

public sealed class M03_8_PersonalTransactionHistoryFilterTests
{
    [Fact]
    public async Task History_should_filter_by_date_account_kind_and_category()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m03-8-history-{Guid.NewGuid():N}");

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

            Assert.True(
                schemaVersion >= 11);

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

            var accountA =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto A",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        500m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var accountB =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto B",
                        PersonalAccountTypes.SavingsAccount,
                        "PLN",
                        100m),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var annaAccount =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto Anny",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        0m),
                    annaUserId,
                    Guid.NewGuid().ToString("N"));

            var date1 =
                DateTime.SpecifyKind(
                    DateTime.UtcNow.Date
                        .AddDays(-10)
                        .AddHours(12),
                    DateTimeKind.Utc);

            var date2 =
                DateTime.SpecifyKind(
                    DateTime.UtcNow.Date
                        .AddDays(-5)
                        .AddHours(12),
                    DateTimeKind.Utc);

            var date3 =
                DateTime.SpecifyKind(
                    DateTime.UtcNow.Date
                        .AddDays(-1)
                        .AddHours(12),
                    DateTimeKind.Utc);

            await service.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    accountA,
                    PersonalTransactionKinds.Income,
                    1000m,
                    date1,
                    "Pensja",
                    PersonalFinanceCategories.Salary,
                    "Pracodawca"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var foodExpenseId =
                await service.PostOwnOperationAsync(
                    new PostPersonalOperationRequest(
                        accountA,
                        PersonalTransactionKinds.Expense,
                        100m,
                        date2,
                        "Zakupy",
                        PersonalFinanceCategories.Food,
                        "Sklep"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            await service.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    accountB,
                    PersonalTransactionKinds.Expense,
                    50m,
                    date3,
                    "Bilet",
                    PersonalFinanceCategories.Transport,
                    "Transport"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await service.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    annaAccount,
                    PersonalTransactionKinds.Income,
                    999m,
                    date2,
                    "Prywatny wpływ Anny",
                    PersonalFinanceCategories.OtherIncome,
                    "Płatnik Anny"),
                annaUserId,
                Guid.NewGuid().ToString("N"));

            var byDate =
                await service.GetOwnTransactionHistoryAsync(
                    new PersonalTransactionHistoryFilter(
                        date2.Date,
                        date2.Date,
                        null,
                        null,
                        null),
                    administrator.UserId);

            Assert.Single(byDate.Items);
            Assert.Equal(
                foodExpenseId,
                byDate.Items[0].TransactionId);

            var byAccount =
                await service.GetOwnTransactionHistoryAsync(
                    new PersonalTransactionHistoryFilter(
                        null,
                        null,
                        accountB,
                        null,
                        null),
                    administrator.UserId);

            Assert.Equal(
                2,
                byAccount.TotalCount);

            Assert.All(
                byAccount.Items,
                x => Assert.Equal(
                    accountB,
                    x.AccountId));

            Assert.Contains(
                byAccount.Items,
                x =>
                    x.KindCode ==
                    PersonalTransactionKinds.Expense &&
                    x.Description ==
                    "Bilet");

            var byKind =
                await service.GetOwnTransactionHistoryAsync(
                    new PersonalTransactionHistoryFilter(
                        null,
                        null,
                        null,
                        PersonalTransactionKinds.Expense,
                        null),
                    administrator.UserId);

            Assert.Equal(
                2,
                byKind.TotalCount);

            Assert.All(
                byKind.Items,
                x => Assert.Equal(
                    PersonalTransactionKinds.Expense,
                    x.KindCode));

            var byCategory =
                await service.GetOwnTransactionHistoryAsync(
                    new PersonalTransactionHistoryFilter(
                        null,
                        null,
                        null,
                        null,
                        PersonalFinanceCategories.Food),
                    administrator.UserId);

            Assert.Single(byCategory.Items);
            Assert.Equal(
                foodExpenseId,
                byCategory.Items[0].TransactionId);
            Assert.Equal(
                "Żywność",
                byCategory.Items[0].CategoryNamePl);
            Assert.Equal(
                "Sklep",
                byCategory.Items[0].Counterparty);

            var combined =
                await service.GetOwnTransactionHistoryAsync(
                    new PersonalTransactionHistoryFilter(
                        date2.Date,
                        date3.Date,
                        null,
                        PersonalTransactionKinds.Expense,
                        null),
                    administrator.UserId);

            Assert.Equal(
                2,
                combined.TotalCount);

            Assert.DoesNotContain(
                combined.Items,
                x =>
                    x.Description ==
                    "Prywatny wpływ Anny");
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
    public async Task History_should_reject_filtering_by_another_users_account()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m03-8-scope-{Guid.NewGuid():N}");

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

            var annaAccount =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto Anny",
                        PersonalAccountTypes.BankAccount,
                        "PLN",
                        100m),
                    annaUserId,
                    Guid.NewGuid().ToString("N"));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () =>
                    service.GetOwnTransactionHistoryAsync(
                        new PersonalTransactionHistoryFilter(
                            null,
                            null,
                            annaAccount,
                            null,
                            null),
                        administrator.UserId));
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
