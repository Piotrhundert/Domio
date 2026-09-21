using Domio.Application.Authentication;
using Domio.Application.FamilyFinance;
using Domio.Application.HouseholdFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.FamilyFinance;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_8_4_ChildIncomeTests
{
    [Fact]
    public async Task Child_benefits_should_be_planned_as_income_and_never_as_family_expense()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-8-4-child-income-{Guid.NewGuid():N}");
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

            Assert.InRange(schemaVersion, 20, int.MaxValue);
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
            await householdService.CreateAccountAsync(
                new CreateHouseholdAccountRequest(
                    "Konto domu",
                    HouseholdAccountTypes.Bank,
                    "PLN",
                    0m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var familyService = new FamilyFinanceService(dbContext, auditService);
            var budgetService = new FamilyBudgetService(dbContext, auditService);

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

            var activeFrom = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

            await familyService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    familyGroupId,
                    childPersonId,
                    FamilyIncomeKinds.ChildBenefit800Plus,
                    null,
                    800m,
                    FamilyRecurringFrequencies.Monthly,
                    5,
                    activeFrom,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await familyService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    familyGroupId,
                    childPersonId,
                    FamilyIncomeKinds.CareAllowance,
                    null,
                    215.84m,
                    FamilyRecurringFrequencies.Monthly,
                    15,
                    activeFrom,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await familyService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    familyGroupId,
                    childPersonId,
                    FamilyIncomeKinds.CareBenefit,
                    null,
                    3386m,
                    FamilyRecurringFrequencies.Monthly,
                    20,
                    activeFrom,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var familyOverview = await familyService.GetOverviewAsync(
                administrator.UserId,
                familyGroupId,
                2026,
                9);

            var child = Assert.Single(
                familyOverview.Members.Where(x => x.PersonId == childPersonId));

            Assert.Equal(FamilyRoles.Child, child.FamilyRoleCode);
            Assert.Equal(4401.84m, child.PlannedIncome!.Value);
            Assert.Null(child.ActualIncome);
            Assert.Equal(4401.84m, familyOverview.PlannedIncomeTotal);
            Assert.Equal(3, familyOverview.ChildIncomeRules.Count);
            Assert.All(
                familyOverview.ChildIncomeRules,
                x => Assert.True(x.AppliesInSelectedMonth));

            var familyBudget = await budgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                2026,
                9);

            Assert.Equal(0m, familyBudget.PlannedExpenseTotal);
            Assert.Empty(familyBudget.FamilyRules);
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
