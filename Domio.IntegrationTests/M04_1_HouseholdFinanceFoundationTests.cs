using Domio.Application.Authentication;
using Domio.Application.HouseholdFinance;
using Domio.Application.Users;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_1_HouseholdFinanceFoundationTests
{
    [Fact]
    public async Task Household_finances_should_create_accounts_book_entries_and_block_negative_balance()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"domio-m04-1-{Guid.NewGuid():N}");

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

            var userManagement =
                new UserManagementService(
                    dbContext,
                    auditService);

            var householdMemberUserId =
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
                new HouseholdFinanceService(
                    dbContext,
                    auditService);

            var bankAccountId =
                await service.CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        "Konto Dom 1",
                        HouseholdAccountTypes.Bank,
                        "pln",
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

            var expenseId =
                await service.PostOperationAsync(
                    new PostHouseholdOperationRequest(
                        bankAccountId,
                        HouseholdEntryTypes.Expense,
                        200.25m,
                        DateTime.UtcNow,
                        HouseholdFinanceCategories.Utilities,
                        "Rachunek testowy"),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            await service.PostOperationAsync(
                new PostHouseholdOperationRequest(
                    bankAccountId,
                    HouseholdEntryTypes.Income,
                    50.10m,
                    DateTime.UtcNow,
                    HouseholdFinanceCategories.HouseholdIncome,
                    "Wpływ testowy"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var overview =
                await service.GetOverviewAsync(
                    administrator.UserId);

            Assert.NotNull(
                overview);

            Assert.Equal(
                "Gospodarstwo domowe",
                overview!.HouseholdName);

            Assert.Equal(
                "PLN",
                overview.CurrencyCode);

            Assert.Equal(
                2,
                overview.Accounts.Count);

            Assert.Equal(
                849.85m,
                overview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            Assert.Equal(
                100m,
                overview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            cashAccountId)
                    .Balance);

            var expense =
                overview.RecentEntries
                    .Single(x =>
                        x.EntryId ==
                            expenseId);

            Assert.Equal(
                -200.25m,
                expense.Amount);

            Assert.Equal(
                "Media i rachunki",
                expense.CategoryNamePl);

            Assert.Equal(
                "Manual",
                expense.SourceType);

            var balanceBeforeRejected =
                overview.Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance;

            var insufficient =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () =>
                        service.PostOperationAsync(
                            new PostHouseholdOperationRequest(
                                bankAccountId,
                                HouseholdEntryTypes.Expense,
                                1000m,
                                DateTime.UtcNow,
                                HouseholdFinanceCategories.OtherExpense,
                                "Za duży wydatek"),
                            administrator.UserId,
                            Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "Niewystarczające środki",
                insufficient.Message);

            Assert.Equal(
                balanceBeforeRejected,
                (await service.GetOverviewAsync(
                    administrator.UserId))!
                    .Accounts
                    .Single(x =>
                        x.AccountId ==
                            bankAccountId)
                    .Balance);

            Assert.Equal(
                1,
                await dbContext.Households
                    .CountAsync());

            Assert.Equal(
                1,
                await dbContext.HouseholdMembers
                    .CountAsync());

            Assert.Equal(
                2,
                await dbContext.HouseholdAccounts
                    .CountAsync());

            Assert.Equal(
                4,
                await dbContext.HouseholdEntries
                    .CountAsync());

            var memberOverview =
                await service.GetOverviewAsync(
                    householdMemberUserId);

            Assert.Null(
                memberOverview);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () =>
                    service.CreateAccountAsync(
                        new CreateHouseholdAccountRequest(
                            "Konto Anny",
                            HouseholdAccountTypes.Bank,
                            "PLN",
                            0m),
                        householdMemberUserId,
                        Guid.NewGuid().ToString("N")));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () =>
                    service.GetOverviewAsync(
                        guestUserId));

            var householdAudit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.EventType.StartsWith(
                            "M04.1."))
                    .ToListAsync();

            Assert.NotEmpty(
                householdAudit);

            Assert.Contains(
                householdAudit,
                x =>
                    x.EventType ==
                    "M04.1.HouseholdCreated");

            Assert.Contains(
                householdAudit,
                x =>
                    x.EventType ==
                    "M04.1.HouseholdAccountCreated");

            Assert.Contains(
                householdAudit,
                x =>
                    x.EventType ==
                    "M04.1.HouseholdEntryPosted");

            Assert.All(
                householdAudit,
                entry =>
                {
                    var auditText =
                        string.Join(
                            " ",
                            entry.Description,
                            entry.OldValuesJson,
                            entry.NewValuesJson);

                    Assert.DoesNotContain(
                        "200.25",
                        auditText);

                    Assert.DoesNotContain(
                        "Rachunek testowy",
                        auditText);

                    Assert.DoesNotContain(
                        "Konto Dom 1",
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
    public void Household_money_should_remain_exact_to_grosz()
    {
        Assert.Throws<ArgumentException>(
            () =>
                HouseholdFinanceMoney.ToMinorUnits(
                    12.345m));

        Assert.Equal(
            1234,
            HouseholdFinanceMoney.ToMinorUnits(
                12.34m));

        Assert.Equal(
            12.34m,
            HouseholdFinanceMoney.FromMinorUnits(
                1234));
    }
}
