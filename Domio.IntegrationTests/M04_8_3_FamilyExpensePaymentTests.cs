using System.Data;
using Domio.Application.Authentication;
using Domio.Application.FamilyFinance;
using Domio.Application.HouseholdFinance;
using Domio.Application.PersonalFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.FamilyFinance;
using Domio.Infrastructure.HouseholdFinance;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.PersonalFinance;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M04_8_3_FamilyExpensePaymentTests
{
    [Fact]
    public async Task Planned_family_expense_can_be_paid_from_personal_or_household_account_once()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m04-8-3-family-payment-{Guid.NewGuid():N}");
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

            var householdAccountId = await householdService.CreateAccountAsync(
                new CreateHouseholdAccountRequest(
                    "Konto domu",
                    HouseholdAccountTypes.Bank,
                    "PLN",
                    1000m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var personalAccountId = await personalService.CreateOwnAccountAsync(
                new CreatePersonalAccountRequest(
                    "Konto osobiste",
                    PersonalAccountTypes.BankAccount,
                    "PLN",
                    500m),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var familyGroupId = await familyService.CreateGroupAsync(
                new CreateFamilyGroupRequest("Rodzina testowa"),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var today = DateTime.UtcNow.Date;
            var monthStart = new DateTime(
                today.Year,
                today.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc);

            await familyBudgetService.CreateRecurringExpenseAsync(
                new CreateFamilyRecurringExpenseRequest(
                    familyGroupId,
                    "Chat GPT",
                    FamilyBudgetCategories.Subscription,
                    120m,
                    FamilyRecurringFrequencies.Monthly,
                    Math.Min(today.Day, 28),
                    null,
                    monthStart,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var beforePayment = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                today.Year,
                today.Month);

            var firstRule = Assert.Single(beforePayment.FamilyRules);
            Assert.True(firstRule.CanPay);
            Assert.NotNull(firstRule.OccurrenceId);
            Assert.Equal(120m, beforePayment.PlannedExpenseTotal);
            Assert.Equal(0m, beforePayment.ActualExpenseTotal);

            var paymentForm = await familyBudgetService.GetExpensePaymentFormAsync(
                familyGroupId,
                firstRule.OccurrenceId!.Value,
                administrator.UserId);

            Assert.NotNull(paymentForm);
            Assert.Contains(
                paymentForm!.Accounts,
                x =>
                    x.PaymentAccountType == FamilyExpensePaymentAccountTypes.PersonalAccount &&
                    x.AccountId == personalAccountId);
            Assert.Contains(
                paymentForm.Accounts,
                x =>
                    x.PaymentAccountType == FamilyExpensePaymentAccountTypes.HouseholdAccount &&
                    x.AccountId == householdAccountId);

            var personalPayment = await familyBudgetService.PayExpenseAsync(
                new PayFamilyExpenseRequest(
                    familyGroupId,
                    firstRule.OccurrenceId.Value,
                    FamilyExpensePaymentAccountTypes.PersonalAccount,
                    personalAccountId,
                    today),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            Assert.Equal(FamilyBudgetSourceTypes.PersonalTransaction, personalPayment.SourceType);

            var personalBalance = (await personalService.GetOwnOverviewAsync(administrator.UserId))
                .Accounts.Single(x => x.AccountId == personalAccountId).Balance;
            var householdBalance = (await householdService.GetOverviewAsync(administrator.UserId))!
                .Accounts.Single(x => x.AccountId == householdAccountId).Balance;

            Assert.Equal(380m, personalBalance);
            Assert.Equal(1000m, householdBalance);

            var afterPersonalPayment = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                today.Year,
                today.Month);

            Assert.Equal(120m, afterPersonalPayment.PlannedExpenseTotal);
            Assert.Equal(120m, afterPersonalPayment.ActualExpenseTotal);
            var paidFirstRule = Assert.Single(afterPersonalPayment.FamilyRules);
            Assert.False(paidFirstRule.CanPay);
            Assert.Equal(FamilyRecurringOccurrenceStatuses.Paid, paidFirstRule.OccurrenceStatusCode);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                familyBudgetService.PayExpenseAsync(
                    new PayFamilyExpenseRequest(
                        familyGroupId,
                        firstRule.OccurrenceId.Value,
                        FamilyExpensePaymentAccountTypes.PersonalAccount,
                        personalAccountId,
                        today),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            await familyBudgetService.CreateRecurringExpenseAsync(
                new CreateFamilyRecurringExpenseRequest(
                    familyGroupId,
                    "Ubezpieczenie rodzinne",
                    FamilyBudgetCategories.Insurance,
                    80m,
                    FamilyRecurringFrequencies.Once,
                    Math.Min(today.Day, 28),
                    null,
                    monthStart,
                    null),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var withSecondExpense = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                today.Year,
                today.Month);

            var secondRule = withSecondExpense.FamilyRules
                .Single(x => x.Name == "Ubezpieczenie rodzinne");
            Assert.True(secondRule.CanPay);
            Assert.NotNull(secondRule.OccurrenceId);

            var householdPayment = await familyBudgetService.PayExpenseAsync(
                new PayFamilyExpenseRequest(
                    familyGroupId,
                    secondRule.OccurrenceId!.Value,
                    FamilyExpensePaymentAccountTypes.HouseholdAccount,
                    householdAccountId,
                    today),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            Assert.Equal(FamilyBudgetSourceTypes.HouseholdEntry, householdPayment.SourceType);

            var finalPersonalBalance = (await personalService.GetOwnOverviewAsync(administrator.UserId))
                .Accounts.Single(x => x.AccountId == personalAccountId).Balance;
            var finalHouseholdBalance = (await householdService.GetOverviewAsync(administrator.UserId))!
                .Accounts.Single(x => x.AccountId == householdAccountId).Balance;
            var finalFamilyOverview = await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                administrator.UserId,
                today.Year,
                today.Month);

            Assert.Equal(380m, finalPersonalBalance);
            Assert.Equal(920m, finalHouseholdBalance);
            Assert.Equal(200m, finalFamilyOverview.PlannedExpenseTotal);
            Assert.Equal(200m, finalFamilyOverview.ActualExpenseTotal);
            Assert.Equal(2, await CountRowsAsync(dbContext, "FamilyExpensePayments"));
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

    private static async Task<long> CountRowsAsync(
        DomioDbContext dbContext,
        string tableName)
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
            command.CommandText = $"SELECT COUNT(1) FROM {tableName};";
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
