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

public sealed class M03_1_PersonalFinanceFoundationTests
{
    [Fact]
    public async Task Personal_finances_should_be_private_exact_and_block_insufficient_funds()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m03-1-{Guid.NewGuid():N}");

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

            Assert.True(schemaVersion >= 8);
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

            var guestUserId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Gosc",
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

            var accountId =
                await service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto główne",
                        PersonalAccountTypes.BankAccount,
                        "pln",
                        1000.00m),
                    annaUserId,
                    Guid.NewGuid().ToString("N"));

            await service.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    accountId,
                    PersonalTransactionKinds.Expense,
                    200.25m,
                    DateTime.UtcNow,
                    "Zakupy"),
                annaUserId,
                Guid.NewGuid().ToString("N"));

            await service.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    accountId,
                    PersonalTransactionKinds.Income,
                    50.10m,
                    DateTime.UtcNow,
                    "Zwrot"),
                annaUserId,
                Guid.NewGuid().ToString("N"));

            var annaOverview =
                await service.GetOwnOverviewAsync(
                    annaUserId);

            var annaAccount =
                Assert.Single(
                    annaOverview.Accounts);

            Assert.Equal(
                849.85m,
                annaAccount.Balance);
            Assert.Equal(
                "PLN",
                annaAccount.CurrencyCode);

            var piotrOverview =
                await service.GetOwnOverviewAsync(
                    piotrUserId);

            Assert.Empty(
                piotrOverview.Accounts);

            var administratorOverview =
                await service.GetOwnOverviewAsync(
                    administrator.UserId);

            Assert.Empty(
                administratorOverview.Accounts);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.PostOwnOperationAsync(
                    new PostPersonalOperationRequest(
                        accountId,
                        PersonalTransactionKinds.Expense,
                        1m,
                        DateTime.UtcNow,
                        "Próba dostępu do cudzego konta"),
                    piotrUserId,
                    Guid.NewGuid().ToString("N")));

            var insufficient =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.PostOwnOperationAsync(
                        new PostPersonalOperationRequest(
                            accountId,
                            PersonalTransactionKinds.Expense,
                            1000m,
                            DateTime.UtcNow,
                            "Za duży wydatek"),
                        annaUserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "Niewystarczające środki",
                insufficient.Message);

            var balanceAfterRejectedOperation =
                (await service.GetOwnOverviewAsync(
                    annaUserId))
                .Accounts
                .Single()
                .Balance;

            Assert.Equal(
                849.85m,
                balanceAfterRejectedOperation);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
                        "Konto gościa",
                        PersonalAccountTypes.Cash,
                        "PLN",
                        10m),
                    guestUserId,
                    Guid.NewGuid().ToString("N")));

            var personalAudit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.EventType.StartsWith(
                            "M03.1."))
                    .ToListAsync();

            Assert.NotEmpty(personalAudit);

            Assert.All(
                personalAudit,
                entry =>
                {
                    var auditText =
                        string.Join(
                            " ",
                            entry.Description,
                            entry.OldValuesJson,
                            entry.NewValuesJson);

                    Assert.DoesNotContain(
                        "1000.00",
                        auditText);
                    Assert.DoesNotContain(
                        "200.25",
                        auditText);
                    Assert.DoesNotContain(
                        "Zakupy",
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
    public void Money_should_reject_more_than_two_decimal_places()
    {
        Assert.Throws<ArgumentException>(
            () => PersonalFinanceMoney.ToMinorUnits(
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
