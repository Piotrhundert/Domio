using System.Data;
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

public sealed class M04_8_2_FamilyBudgetTests
{
    [Fact]
    public async Task Family_plan_links_child_cost_and_corrections_should_not_double_book_money()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-8-2-family-budget-{Guid.NewGuid():N}");
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

            Assert.InRange(schemaVersion, 19, int.MaxValue);
            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());

            var auditService = new AuditService(dbContext);
            var authenticationService = new AccountAuthenticationService(dbContext, auditService);
            var administrator = await authenticationService.InitializeFirstAdministratorAsync(
                new FirstAdministratorSetupRequest(
                    "Piotr",
                    "Testowy",
                    "admin",
                    "DomioTest123"),
                Guid.NewGuid().ToString("N"));

            var householdService = new HouseholdFinanceService(dbContext, auditService);
            var personalService = new PersonalFinanceService(dbContext, auditService);
            var familyService = new FamilyFinanceService(dbContext, auditService);
            var familyBudgetService = new FamilyBudgetService(dbContext, auditService);

            await householdService.CreateAccountAsync(
                new CreateHouseholdAccountRequest(
                    "Konto domu",
                    HouseholdAccountTypes.Bank,
                    "PLN",
                    1000m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var privateAccountId = await personalService.CreateOwnAccountAsync(
                new CreatePersonalAccountRequest(
                    "Konto prywatne",
                    PersonalAccountTypes.BankAccount,
                    "PLN",
                    500m),
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
                    FirstName = "Tymek",
                    LastName = "Testowy",
                    DisplayName = "Tymek",
                    PersonTypeCode = "Child",
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

            await familyService.UpdateOwnSharingAsync(
                new UpdateFamilySharingRequest(
                    familyGroupId,
                    SharePlannedIncome: false,
                    ShareActualIncome: false,
                    ShareFamilyExpenses: true,
                    ShareRecurringRules: true),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var balanceBeforePlan = (await personalService.GetOwnOverviewAsync(administrator.UserId))
                .Accounts.Single(x => x.AccountId == privateAccountId).Balance;

            await familyBudgetService.CreateRecurringExpenseAsync(
                new CreateFamilyRecurringExpenseRequest(
                    familyGroupId,
                    "Basen Tymka",
                    FamilyBudgetCategories.Child,
                    180m,
                    FamilyRecurringFrequencies.Monthly,
                    10,
                    childPersonId,
                    new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var firstOverview = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                now.Year,
                now.Month);

            var secondOverview = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                now.Year,
                now.Month);

            Assert.Equal(180m, firstOverview.PlannedExpenseTotal);
            Assert.Equal(180m, secondOverview.PlannedExpenseTotal);
            Assert.Equal(
                180m,
                secondOverview.ChildCosts.Single(x => x.PersonId == childPersonId).PlannedAmount);

            var occurrenceCount = await CountOccurrencesAsync(
                dbContext,
                familyGroupId,
                $"{now.Year:D4}-{now.Month:D2}");
            Assert.Equal(1, occurrenceCount);

            var balanceAfterPlan = (await personalService.GetOwnOverviewAsync(administrator.UserId))
                .Accounts.Single(x => x.AccountId == privateAccountId).Balance;
            Assert.Equal(balanceBeforePlan, balanceAfterPlan);

            var everyTwoMonthsStart = new DateTime(
                now.Year,
                now.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc).AddMonths(1);

            await familyBudgetService.CreateRecurringExpenseAsync(
                new CreateFamilyRecurringExpenseRequest(
                    familyGroupId,
                    "Przegląd cykliczny co dwa miesiące",
                    FamilyBudgetCategories.Home,
                    25m,
                    FamilyRecurringFrequencies.Every2Months,
                    15,
                    null,
                    everyTwoMonthsStart,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var firstEveryTwoMonthsPeriod = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                everyTwoMonthsStart.Year,
                everyTwoMonthsStart.Month);

            var skippedEveryTwoMonthsPeriodDate = everyTwoMonthsStart.AddMonths(1);
            var skippedEveryTwoMonthsPeriod = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                skippedEveryTwoMonthsPeriodDate.Year,
                skippedEveryTwoMonthsPeriodDate.Month);

            var secondEveryTwoMonthsPeriodDate = everyTwoMonthsStart.AddMonths(2);
            var secondEveryTwoMonthsPeriod = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                secondEveryTwoMonthsPeriodDate.Year,
                secondEveryTwoMonthsPeriodDate.Month);

            Assert.Equal(205m, firstEveryTwoMonthsPeriod.PlannedExpenseTotal);
            Assert.Equal(180m, skippedEveryTwoMonthsPeriod.PlannedExpenseTotal);
            Assert.Equal(205m, secondEveryTwoMonthsPeriod.PlannedExpenseTotal);

            var expenseId = await personalService.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    privateAccountId,
                    PersonalTransactionKinds.Expense,
                    100m,
                    now,
                    "Buty sportowe dla dziecka",
                    PersonalFinanceCategories.Leisure,
                    "Sklep testowy"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var linkId = await familyBudgetService.LinkSourceAsync(
                new CreateFamilyBudgetLinkRequest(
                    familyGroupId,
                    FamilyBudgetSourceTypes.PersonalTransaction,
                    expenseId,
                    FamilyBudgetCategories.Child,
                    childPersonId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            Assert.NotEqual(Guid.Empty, linkId);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                familyBudgetService.LinkSourceAsync(
                    new CreateFamilyBudgetLinkRequest(
                        familyGroupId,
                        FamilyBudgetSourceTypes.PersonalTransaction,
                        expenseId,
                        FamilyBudgetCategories.Child,
                        childPersonId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            var actualOverview = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                now.Year,
                now.Month);

            Assert.Equal(100m, actualOverview.ActualExpenseTotal);
            Assert.Equal(
                100m,
                actualOverview.ChildCosts.Single(x => x.PersonId == childPersonId).ActualAmount);

            await personalService.CorrectOwnTransactionAsync(
                new CorrectPersonalTransactionRequest(
                    expenseId,
                    80m,
                    PersonalFinanceCategories.Leisure),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var correctedOverview = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                now.Year,
                now.Month);

            Assert.Equal(80m, correctedOverview.ActualExpenseTotal);
            Assert.Equal(
                80m,
                correctedOverview.ChildCosts.Single(x => x.PersonId == childPersonId).ActualAmount);

            // Cofnięcie zgody blokuje nowe prywatne powiązania, ale nie usuwa
            // wcześniej jawnie udostępnionej pozycji z historii rodziny.
            await familyService.UpdateOwnSharingAsync(
                new UpdateFamilySharingRequest(
                    familyGroupId,
                    SharePlannedIncome: false,
                    ShareActualIncome: false,
                    ShareFamilyExpenses: false,
                    ShareRecurringRules: false),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var hiddenNewExpenseId = await personalService.PostOwnOperationAsync(
                new PostPersonalOperationRequest(
                    privateAccountId,
                    PersonalTransactionKinds.Expense,
                    10m,
                    now,
                    "Nowy prywatny wydatek po cofnięciu zgody",
                    PersonalFinanceCategories.OtherExpense,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                familyBudgetService.LinkSourceAsync(
                    new CreateFamilyBudgetLinkRequest(
                        familyGroupId,
                        FamilyBudgetSourceTypes.PersonalTransaction,
                        hiddenNewExpenseId,
                        FamilyBudgetCategories.Other,
                        null),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            var afterSharingOff = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                now.Year,
                now.Month);
            Assert.Equal(80m, afterSharingOff.ActualExpenseTotal);

            var finalPrivateBalance = (await personalService.GetOwnOverviewAsync(administrator.UserId))
                .Accounts.Single(x => x.AccountId == privateAccountId).Balance;
            Assert.Equal(410m, finalPrivateBalance);
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

    private static async Task<long> CountOccurrencesAsync(
        DomioDbContext dbContext,
        Guid familyGroupId,
        string periodKey)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(1)
                FROM FamilyRecurringOccurrences
                WHERE FamilyGroupId = $groupId
                  AND PeriodKey = $periodKey;
                """;

            var groupParameter = command.CreateParameter();
            groupParameter.ParameterName = "$groupId";
            groupParameter.Value = familyGroupId;
            command.Parameters.Add(groupParameter);

            var periodParameter = command.CreateParameter();
            periodParameter.ParameterName = "$periodKey";
            periodParameter.Value = periodKey;
            command.Parameters.Add(periodParameter);

            return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
