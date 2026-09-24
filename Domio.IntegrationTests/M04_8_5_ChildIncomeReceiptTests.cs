using Domio.Application.Authentication;
using Domio.Application.FamilyFinance;
using Domio.Application.HouseholdFinance;
using Domio.Application.PersonalFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.FamilyFinance;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_8_5_ChildIncomeReceiptTests
{
    [Fact]
    public async Task Child_income_receipt_should_book_to_selected_account_and_count_once_for_child()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-8-5-child-receipt-{Guid.NewGuid():N}");
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

            Assert.InRange(schemaVersion, 21, int.MaxValue);
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

            var householdService = new HouseholdFinanceService(dbContext, auditService);
            var personalService = new PersonalFinanceService(dbContext, auditService);
            var familyService = new FamilyFinanceService(dbContext, auditService);

            var householdAccountId = await householdService.CreateAccountAsync(
                new CreateHouseholdAccountRequest(
                    "Konto domu",
                    HouseholdAccountTypes.Bank,
                    "PLN",
                    100m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var personalAccountId = await personalService.CreateOwnAccountAsync(
                new CreatePersonalAccountRequest(
                    "Konto osobiste",
                    PersonalAccountTypes.BankAccount,
                    "PLN",
                    50m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var familyGroupId = await familyService.CreateGroupAsync(
                new CreateFamilyGroupRequest("Rodzina testowa"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

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

            var allowanceRuleId = await familyService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    familyGroupId,
                    childPersonId,
                    FamilyIncomeKinds.CareAllowance,
                    null,
                    215.84m,
                    FamilyRecurringFrequencies.Monthly,
                    5,
                    monthStart,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var benefitRuleId = await familyService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    familyGroupId,
                    childPersonId,
                    FamilyIncomeKinds.ChildBenefit800Plus,
                    null,
                    800m,
                    FamilyRecurringFrequencies.Monthly,
                    10,
                    monthStart,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var personalForm = await familyService.GetChildIncomeReceiptFormAsync(
                familyGroupId,
                allowanceRuleId,
                now.Year,
                now.Month,
                administrator.UserId);

            Assert.NotNull(personalForm);
            Assert.Contains(
                personalForm!.Accounts,
                x =>
                    x.AccountType == FamilyIncomeReceiptAccountTypes.PersonalAccount &&
                    x.AccountId == personalAccountId);

            await familyService.ConfirmChildIncomeReceiptAsync(
                new ConfirmFamilyChildIncomeReceiptRequest(
                    familyGroupId,
                    allowanceRuleId,
                    now.Year,
                    now.Month,
                    FamilyIncomeReceiptAccountTypes.PersonalAccount,
                    personalAccountId,
                    now.Date),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var personalBalance = (await personalService.GetOwnOverviewAsync(
                    administrator.UserId))
                .Accounts
                .Single(x => x.AccountId == personalAccountId)
                .Balance;

            Assert.Equal(265.84m, personalBalance);

            var afterPersonal = await familyService.GetOverviewAsync(
                administrator.UserId,
                familyGroupId,
                now.Year,
                now.Month);

            var administratorRow = Assert.Single(
                afterPersonal.Members.Where(x => x.PersonId == afterPersonal.CurrentPersonId));
            var childRow = Assert.Single(
                afterPersonal.Members.Where(x => x.PersonId == childPersonId));

            Assert.Null(administratorRow.ActualIncome);
            Assert.Equal(215.84m, childRow.ActualIncome!.Value);
            Assert.Equal(215.84m, afterPersonal.ActualIncomeTotal);

            var allowanceSummary = afterPersonal.ChildIncomeRules
                .Single(x => x.RuleId == allowanceRuleId);
            Assert.True(allowanceSummary.IsReceived);
            Assert.Equal(215.84m, allowanceSummary.ReceivedAmount);
            Assert.False(allowanceSummary.CanConfirmReceipt);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                familyService.ConfirmChildIncomeReceiptAsync(
                    new ConfirmFamilyChildIncomeReceiptRequest(
                        familyGroupId,
                        allowanceRuleId,
                        now.Year,
                        now.Month,
                        FamilyIncomeReceiptAccountTypes.PersonalAccount,
                        personalAccountId,
                        now.Date),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            await familyService.ConfirmChildIncomeReceiptAsync(
                new ConfirmFamilyChildIncomeReceiptRequest(
                    familyGroupId,
                    benefitRuleId,
                    now.Year,
                    now.Month,
                    FamilyIncomeReceiptAccountTypes.HouseholdAccount,
                    householdAccountId,
                    now.Date),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var householdBalance = (await householdService.GetOverviewAsync(
                    administrator.UserId))!
                .Accounts
                .Single(x => x.AccountId == householdAccountId)
                .Balance;

            Assert.Equal(900m, householdBalance);

            var finalOverview = await familyService.GetOverviewAsync(
                administrator.UserId,
                familyGroupId,
                now.Year,
                now.Month);

            var finalChild = Assert.Single(
                finalOverview.Members.Where(x => x.PersonId == childPersonId));
            var finalAdmin = Assert.Single(
                finalOverview.Members.Where(x => x.PersonId == finalOverview.CurrentPersonId));

            Assert.Equal(1015.84m, finalChild.ActualIncome!.Value);
            Assert.Null(finalAdmin.ActualIncome);
            Assert.Equal(1015.84m, finalOverview.ActualIncomeTotal);
            Assert.Equal(
                2,
                finalOverview.ChildIncomeRules.Count(x => x.IsReceived));
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
