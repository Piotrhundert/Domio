using Domio.Application.Authentication;
using Domio.Application.FamilyFinance;
using Domio.Application.HouseholdFinance;
using Domio.Application.Users;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.FamilyFinance;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_8_6_FamilySharedAccountTests
{
    [Fact]
    public async Task Shared_account_should_have_one_balance_for_owner_and_coowner_and_accept_child_income()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-8-6-shared-account-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "domio-test.db");

        try
        {
            var options = new DbContextOptionsBuilder<DomioDbContext>()
                .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                .Options;

            await using var dbContext = new DomioDbContext(options);
            await dbContext.Database.MigrateAsync();

            var schemaVersion = await dbContext.SchemaVersions
                .AsNoTracking()
                .Where(x => x.Id == 1)
                .Select(x => x.Version)
                .SingleAsync();

            Assert.InRange(schemaVersion, 22, int.MaxValue);
            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());

            var auditService = new AuditService(dbContext);
            var authenticationService = new AccountAuthenticationService(
                dbContext,
                auditService);

            var administrator = await authenticationService.InitializeFirstAdministratorAsync(
                new FirstAdministratorSetupRequest(
                    "Jan",
                    "Administrator",
                    "admin",
                    "DomioTest123"),
                Guid.NewGuid().ToString("N"));

            var userManagement = new UserManagementService(
                dbContext,
                auditService);

            var coOwnerUserId = await userManagement.CreateUserAsync(
                new CreateUserRequest(
                    null,
                    "Anna",
                    "Współwłaściciel",
                    null,
                    null,
                    "anna-shared@example.test",
                    "DomioAnna123",
                    SystemRoles.HouseholdMemberId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var householdService = new HouseholdFinanceService(
                dbContext,
                auditService);
            var familyService = new FamilyFinanceService(
                dbContext,
                auditService);

            await householdService.CreateAccountAsync(
                new CreateHouseholdAccountRequest(
                    "Konto domu",
                    HouseholdAccountTypes.Bank,
                    "PLN",
                    0m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var familyGroupId = await familyService.CreateGroupAsync(
                new CreateFamilyGroupRequest("Rodzina testowa"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var adminPersonId = await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x => x.Id == administrator.UserId)
                .Select(x => x.PersonId)
                .SingleAsync();

            var coOwnerPersonId = await dbContext.UserAccounts
                .AsNoTracking()
                .Where(x => x.Id == coOwnerUserId)
                .Select(x => x.PersonId)
                .SingleAsync();

            // Współwłaściciel wspólnego konta rodzinnego musi być również
            // aktywnym domownikiem gospodarstwa, bo operacje rodzinne korzystają
            // z kontekstu rodzina + gospodarstwo.
            await householdService.AddHouseholdMemberAsync(
                new AddHouseholdMemberRequest(
                    coOwnerPersonId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await familyService.AddMemberAsync(
                new AddFamilyMemberRequest(
                    familyGroupId,
                    coOwnerPersonId,
                    FamilyRoles.Adult),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var sharedAccountId = await familyService.CreateSharedAccountAsync(
                new CreateFamilySharedAccountRequest(
                    familyGroupId,
                    "Konto Rodzinne",
                    PersonalAccountTypes.BankAccount,
                    1000m,
                    adminPersonId,
                    coOwnerPersonId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var ownerAccounts = await familyService.GetSharedAccountsForUserAsync(
                administrator.UserId);
            var coOwnerAccounts = await familyService.GetSharedAccountsForUserAsync(
                coOwnerUserId);

            var ownerView = Assert.Single(ownerAccounts);
            var coOwnerView = Assert.Single(coOwnerAccounts);

            Assert.Equal(sharedAccountId, ownerView.SharedAccountId);
            Assert.Equal(ownerView.AccountId, coOwnerView.AccountId);
            Assert.Equal(1000m, ownerView.Balance);
            Assert.Equal(1000m, coOwnerView.Balance);
            Assert.Equal(FamilySharedAccountRoles.Owner, ownerView.CurrentPersonRoleCode);
            Assert.Equal(FamilySharedAccountRoles.CoOwner, coOwnerView.CurrentPersonRoleCode);

            await familyService.PostSharedAccountOperationAsync(
                new PostFamilySharedAccountOperationRequest(
                    sharedAccountId,
                    PersonalTransactionKinds.Expense,
                    120m,
                    DateTime.UtcNow.Date,
                    "Zakupy rodzinne",
                    PersonalFinanceCategories.Food,
                    "Sklep"),
                coOwnerUserId,
                Guid.NewGuid().ToString("N"));

            ownerView = Assert.Single(
                await familyService.GetSharedAccountsForUserAsync(
                    administrator.UserId));
            coOwnerView = Assert.Single(
                await familyService.GetSharedAccountsForUserAsync(
                    coOwnerUserId));

            Assert.Equal(880m, ownerView.Balance);
            Assert.Equal(880m, coOwnerView.Balance);

            var now = DateTime.UtcNow;
            var childPersonId = Guid.NewGuid();
            dbContext.People.Add(
                new Person
                {
                    Id = childPersonId,
                    FirstName = "Tymoteusz",
                    LastName = "Testowy",
                    DisplayName = "Tymoteusz Testowy",
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            await dbContext.SaveChangesAsync();

            await familyService.AddMemberAsync(
                new AddFamilyMemberRequest(
                    familyGroupId,
                    childPersonId,
                    FamilyRoles.Child),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var monthStart = new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

            var benefitRuleId = await familyService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    familyGroupId,
                    childPersonId,
                    FamilyIncomeKinds.ChildBenefit800Plus,
                    null,
                    800m,
                    FamilyRecurringFrequencies.Monthly,
                    5,
                    monthStart,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var receiptForm = await familyService.GetChildIncomeReceiptFormAsync(
                familyGroupId,
                benefitRuleId,
                now.Year,
                now.Month,
                coOwnerUserId);

            Assert.NotNull(receiptForm);
            Assert.Contains(
                receiptForm!.Accounts,
                x =>
                    x.AccountType == FamilyIncomeReceiptAccountTypes.FamilySharedAccount &&
                    x.AccountId == ownerView.AccountId);

            await familyService.ConfirmChildIncomeReceiptAsync(
                new ConfirmFamilyChildIncomeReceiptRequest(
                    familyGroupId,
                    benefitRuleId,
                    now.Year,
                    now.Month,
                    FamilyIncomeReceiptAccountTypes.FamilySharedAccount,
                    ownerView.AccountId,
                    now.Date),
                coOwnerUserId,
                Guid.NewGuid().ToString("N"));

            var afterBenefit = Assert.Single(
                await familyService.GetSharedAccountsForUserAsync(
                    administrator.UserId));

            Assert.Equal(1680m, afterBenefit.Balance);

            var familyOverview = await familyService.GetOverviewAsync(
                administrator.UserId,
                familyGroupId,
                now.Year,
                now.Month);

            var childRow = Assert.Single(
                familyOverview.Members.Where(x => x.PersonId == childPersonId));
            var adminRow = Assert.Single(
                familyOverview.Members.Where(x => x.PersonId == adminPersonId));

            Assert.Equal(800m, childRow.ActualIncome!.Value);
            Assert.Null(adminRow.ActualIncome);
            Assert.Equal(800m, familyOverview.ActualIncomeTotal);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
