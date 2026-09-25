using System.Data;
using System.Data.Common;
using System.Globalization;
using Domio.Application.Reporting;
using Domio.Domain.FamilyFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Reporting;

public sealed class FinanceReportingService(
    DomioDbContext dbContext) : IFinanceReportingService
{
    public async Task<FinanceReportDashboard> GetDashboardAsync(
        Guid actorUserId,
        FinanceReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var actorPersonId = await GetActorPersonIdAsync(
            actorUserId,
            cancellationToken);

        var canPersonal = await PermissionEnforcement.HasUserAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinancePersonalViewOwn,
            cancellationToken);
        var canHousehold = await PermissionEnforcement.HasUserAsync(
            dbContext,
            actorUserId,
            SystemPermissions.FinanceHouseholdView,
            cancellationToken);
        var canFamily = await PermissionEnforcement.HasUserAsync(
            dbContext,
            actorUserId,
            FamilyFinancePermissions.View,
            cancellationToken);

        var householdId = canHousehold
            ? await dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(x => x.PersonId == actorPersonId && x.IsActive)
                .Select(x => (Guid?)x.HouseholdId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        IReadOnlyList<FinanceReportFamilyOption> familyOptions = canFamily
            ? await GetFamilyOptionsAsync(actorPersonId, cancellationToken)
            : [];

        var scopeOptions = new[]
        {
            new FinanceReportScopeOption(
                FinanceReportScopes.Personal,
                FinanceReportScopes.GetNamePl(FinanceReportScopes.Personal),
                canPersonal),
            new FinanceReportScopeOption(
                FinanceReportScopes.Household,
                FinanceReportScopes.GetNamePl(FinanceReportScopes.Household),
                canHousehold && householdId.HasValue),
            new FinanceReportScopeOption(
                FinanceReportScopes.Family,
                FinanceReportScopes.GetNamePl(FinanceReportScopes.Family),
                canFamily && familyOptions.Count > 0)
        };

        var requestedScope = FinanceReportScopes.IsValid(query.ScopeCode)
            ? query.ScopeCode!
            : null;
        var selectedScope = scopeOptions
            .FirstOrDefault(x => x.ScopeCode == requestedScope && x.IsAvailable)?
            .ScopeCode
            ?? scopeOptions.FirstOrDefault(x => x.IsAvailable)?.ScopeCode
            ?? throw new UnauthorizedAccessException(
                "Użytkownik nie ma dostępu do żadnego zakresu raportów finansowych.");

        var months = Math.Clamp(query.Months, 1, 36);
        var currentMonth = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        var fromUtc = currentMonth.AddMonths(-(months - 1));
        var toUtc = currentMonth.AddMonths(1);

        Guid? familyGroupId = null;
        string? familyGroupName = null;

        if (selectedScope == FinanceReportScopes.Family)
        {
            var selectedFamily = familyOptions.FirstOrDefault(x =>
                    query.FamilyGroupId.HasValue &&
                    x.FamilyGroupId == query.FamilyGroupId.Value)
                ?? familyOptions.First();
            familyGroupId = selectedFamily.FamilyGroupId;
            familyGroupName = selectedFamily.Name;
        }

        IReadOnlyList<string> availableCurrencies = selectedScope switch
        {
            FinanceReportScopes.Personal =>
                await GetPersonalCurrenciesAsync(actorPersonId, cancellationToken),
            FinanceReportScopes.Household when householdId.HasValue =>
                await GetHouseholdCurrenciesAsync(householdId.Value, cancellationToken),
            _ => ["PLN"]
        };

        if (availableCurrencies.Count == 0)
        {
            availableCurrencies = ["PLN"];
        }

        var requestedCurrency = NormalizeCurrency(query.CurrencyCode);
        var currency = availableCurrencies.Contains(
                requestedCurrency,
                StringComparer.OrdinalIgnoreCase)
            ? requestedCurrency
            : availableCurrencies[0];

        ScopeData data = selectedScope switch
        {
            FinanceReportScopes.Personal =>
                await BuildPersonalAsync(
                    actorPersonId,
                    currency,
                    fromUtc,
                    toUtc,
                    months,
                    cancellationToken),
            FinanceReportScopes.Household when householdId.HasValue =>
                await BuildHouseholdAsync(
                    householdId.Value,
                    currency,
                    fromUtc,
                    toUtc,
                    months,
                    cancellationToken),
            FinanceReportScopes.Family when familyGroupId.HasValue =>
                await BuildFamilyAsync(
                    familyGroupId.Value,
                    actorPersonId,
                    currency,
                    fromUtc,
                    toUtc,
                    months,
                    cancellationToken),
            _ => throw new InvalidOperationException(
                "Nie można przygotować wybranego raportu.")
        };

        var totalIncome = data.Monthly.Sum(x => x.Income);
        var totalExpense = data.Monthly.Sum(x => x.Expense);
        var totalNet = totalIncome - totalExpense;
        var averageIncome = months > 0 ? totalIncome / months : 0m;
        var averageExpense = months > 0 ? totalExpense / months : 0m;
        var savingsRate = totalIncome == 0m
            ? 0m
            : decimal.Round(totalNet / totalIncome * 100m, 1);
        decimal? runway = averageExpense > 0m
            ? decimal.Round(data.CurrentBalance / averageExpense, 1)
            : null;
        var largest = data.TopExpenses.FirstOrDefault();

        var kpis = new FinanceReportKpis(
            data.CurrentBalance,
            totalIncome,
            totalExpense,
            totalNet,
            averageIncome,
            averageExpense,
            savingsRate,
            runway,
            largest?.Amount ?? 0m,
            largest?.Label);

        return new FinanceReportDashboard(
            selectedScope,
            FinanceReportScopes.GetNamePl(selectedScope),
            currency,
            fromUtc,
            toUtc.AddDays(-1),
            months,
            familyGroupId,
            familyGroupName,
            scopeOptions,
            familyOptions,
            availableCurrencies,
            kpis,
            data.Monthly,
            data.Categories,
            data.Accounts,
            data.TopExpenses,
            data.Obligations,
            data.Areas);
    }

    public async Task<FinanceSimulationResult> SimulateAsync(
        Guid actorUserId,
        FinanceSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateSimulation(request);

        var dashboard = await GetDashboardAsync(
            actorUserId,
            new FinanceReportQuery(
                request.ScopeCode,
                request.HistoryMonths,
                request.CurrencyCode,
                request.FamilyGroupId),
            cancellationToken);

        var baselineIncome = dashboard.Kpis.AverageMonthlyIncome;
        var baselineExpense = dashboard.Kpis.AverageMonthlyExpense;
        var baselineBalance = dashboard.Kpis.CurrentBalance;
        var scenarioBalance = dashboard.Kpis.CurrentBalance;
        var result = new List<FinanceSimulationMonth>();
        int? firstNegativeMonth = null;
        int? targetReachedMonth = null;
        decimal lowestScenario = scenarioBalance;

        for (var monthNumber = 1;
             monthNumber <= request.HorizonMonths;
             monthNumber++)
        {
            baselineBalance += baselineIncome - baselineExpense;

            var incomeGrowthFactor = CompoundFactor(
                request.AnnualIncomeGrowthPercent,
                monthNumber - 1);
            var expenseGrowthFactor = CompoundFactor(
                request.AnnualExpenseInflationPercent,
                monthNumber - 1);

            var scenarioIncome =
                baselineIncome * (1m + request.IncomeChangePercent / 100m) *
                incomeGrowthFactor + request.ExtraMonthlyIncome;
            var scenarioExpense =
                baselineExpense * (1m + request.ExpenseChangePercent / 100m) *
                expenseGrowthFactor + request.ExtraMonthlyExpense;

            if (monthNumber == request.OneTimeMonth)
            {
                scenarioIncome += request.OneTimeIncome;
                scenarioExpense += request.OneTimeExpense;
            }

            scenarioIncome = Math.Max(0m, scenarioIncome);
            scenarioExpense = Math.Max(0m, scenarioExpense);
            scenarioBalance += scenarioIncome - scenarioExpense;
            lowestScenario = Math.Min(lowestScenario, scenarioBalance);

            if (!firstNegativeMonth.HasValue && scenarioBalance < 0m)
            {
                firstNegativeMonth = monthNumber;
            }

            if (request.TargetReserve > 0m &&
                !targetReachedMonth.HasValue &&
                scenarioBalance >= request.TargetReserve)
            {
                targetReachedMonth = monthNumber;
            }

            var monthDate = new DateTime(
                DateTime.UtcNow.Year,
                DateTime.UtcNow.Month,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc)
                .AddMonths(monthNumber);

            result.Add(
                new FinanceSimulationMonth(
                    monthNumber,
                    monthDate.ToString("MMM yyyy", CultureInfo.GetCultureInfo("pl-PL")),
                    decimal.Round(baselineIncome, 2),
                    decimal.Round(baselineExpense, 2),
                    decimal.Round(baselineBalance, 2),
                    decimal.Round(scenarioIncome, 2),
                    decimal.Round(scenarioExpense, 2),
                    decimal.Round(scenarioBalance, 2)));
        }

        return new FinanceSimulationResult(
            dashboard.Kpis.CurrentBalance,
            decimal.Round(baselineIncome, 2),
            decimal.Round(baselineExpense, 2),
            decimal.Round(baselineBalance, 2),
            decimal.Round(scenarioBalance, 2),
            decimal.Round(scenarioBalance - baselineBalance, 2),
            decimal.Round(lowestScenario, 2),
            firstNegativeMonth,
            targetReachedMonth,
            request.TargetReserve,
            result);
    }

    private async Task<ScopeData> BuildPersonalAsync(
        Guid personId,
        string currency,
        DateTime fromUtc,
        DateTime toUtc,
        int months,
        CancellationToken cancellationToken)
    {
        var accounts = await dbContext.PersonalFinancialAccounts
            .AsNoTracking()
            .Where(x => x.OwnerPersonId == personId && x.CurrencyCode == currency)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToArray();

        var allBalances = accountIds.Length == 0
            ? new Dictionary<Guid, long>()
            : await dbContext.PersonalFinancialTransactions
                .AsNoTracking()
                .Where(x => x.OwnerPersonId == personId && accountIds.Contains(x.AccountId))
                .GroupBy(x => x.AccountId)
                .Select(x => new { AccountId = x.Key, BalanceMinor = x.Sum(y => y.AmountMinor) })
                .ToDictionaryAsync(x => x.AccountId, x => x.BalanceMinor, cancellationToken);

        PersonalFinancialTransaction[] reportTransactions = accountIds.Length == 0
            ? []
            : await dbContext.PersonalFinancialTransactions
                .AsNoTracking()
                .Where(x =>
                    x.OwnerPersonId == personId &&
                    accountIds.Contains(x.AccountId) &&
                    x.OccurredAtUtc >= fromUtc &&
                    x.OccurredAtUtc < toUtc &&
                    x.KindCode != PersonalTransactionKinds.OpeningBalance &&
                    x.KindCode != PersonalTransactionKinds.TransferIn &&
                    x.KindCode != PersonalTransactionKinds.TransferOut)
                .OrderByDescending(x => x.OccurredAtUtc)
                .ToArrayAsync(cancellationToken);

        var planned = await dbContext.PersonalRecurringOccurrences
            .AsNoTracking()
            .Where(x =>
                x.OwnerPersonId == personId &&
                x.CurrencyCode == currency &&
                x.PlannedDateUtc >= fromUtc &&
                x.PlannedDateUtc < toUtc &&
                x.KindCode == PersonalTransactionKinds.Expense &&
                x.StatusCode != PersonalRecurringOccurrenceStatuses.Cancelled)
            .ToArrayAsync(cancellationToken);

        var monthly = BuildMonthSkeleton(fromUtc, months)
            .Select(x =>
            {
                var items = reportTransactions.Where(t =>
                    t.OccurredAtUtc.Year == x.Year && t.OccurredAtUtc.Month == x.Month);
                var income = SumPositive(items.Select(t => t.AmountMinor));
                var expense = SumNegative(items.Select(t => t.AmountMinor));
                var plannedExpense = planned
                    .Where(p => p.PlannedDateUtc.Year == x.Year && p.PlannedDateUtc.Month == x.Month)
                    .Sum(p => PersonalFinanceMoney.FromMinorUnits(p.PlannedAmountMinor));
                return x with
                {
                    Income = income,
                    Expense = expense,
                    Net = income - expense,
                    PlannedExpense = plannedExpense,
                    ActualExpense = expense
                };
            })
            .ToArray();

        var expenseRows = reportTransactions.Where(x => x.AmountMinor < 0).ToArray();
        var categories = BuildCategories(
            expenseRows
                .GroupBy(x => x.CategoryCode ?? string.Empty)
                .Select(g => new CategoryAmount(
                    g.Key,
                    PersonalFinanceCategories.GetNamePl(g.Key),
                    g.Sum(x => PersonalFinanceMoney.FromMinorUnits(-x.AmountMinor)))));

        var accountSummaries = accounts.Select(x =>
            new FinanceReportAccount(
                x.Id,
                x.Name,
                PersonalAccountTypes.GetNamePl(x.AccountTypeCode),
                x.CurrencyCode,
                PersonalFinanceMoney.FromMinorUnits(allBalances.GetValueOrDefault(x.Id)),
                x.IsActive))
            .ToArray();

        var top = expenseRows
            .OrderBy(x => x.AmountMinor)
            .Take(12)
            .Select(x => new FinanceReportTopExpense(
                x.OccurredAtUtc,
                FirstNonEmpty(x.Counterparty, x.Description, PersonalFinanceCategories.GetNamePl(x.CategoryCode)),
                PersonalFinanceCategories.GetNamePl(x.CategoryCode),
                PersonalFinanceMoney.FromMinorUnits(-x.AmountMinor),
                "PersonalTransaction",
                x.Id,
                null))
            .ToArray();

        return new ScopeData(
            accountSummaries.Sum(x => x.Balance),
            monthly,
            categories,
            accountSummaries,
            top,
            [],
            []);
    }

    private async Task<ScopeData> BuildHouseholdAsync(
        Guid householdId,
        string currency,
        DateTime fromUtc,
        DateTime toUtc,
        int months,
        CancellationToken cancellationToken)
    {
        var accounts = await dbContext.HouseholdAccounts
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId && x.CurrencyCode == currency)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToArray();

        var balances = accountIds.Length == 0
            ? new Dictionary<Guid, long>()
            : await dbContext.HouseholdEntries
                .AsNoTracking()
                .Where(x => x.HouseholdId == householdId && accountIds.Contains(x.AccountId))
                .GroupBy(x => x.AccountId)
                .Select(x => new { AccountId = x.Key, BalanceMinor = x.Sum(y => y.AmountMinor) })
                .ToDictionaryAsync(x => x.AccountId, x => x.BalanceMinor, cancellationToken);

        HouseholdEntry[] entries = accountIds.Length == 0
            ? []
            : await dbContext.HouseholdEntries
                .AsNoTracking()
                .Where(x =>
                    x.HouseholdId == householdId &&
                    accountIds.Contains(x.AccountId) &&
                    x.OccurredAtUtc >= fromUtc &&
                    x.OccurredAtUtc < toUtc &&
                    x.EntryTypeCode != HouseholdEntryTypes.OpeningBalance &&
                    x.EntryTypeCode != HouseholdEntryTypes.TransferIn &&
                    x.EntryTypeCode != HouseholdEntryTypes.TransferOut)
                .OrderByDescending(x => x.OccurredAtUtc)
                .ToArrayAsync(cancellationToken);

        var invoices = await dbContext.HouseholdInvoices
            .AsNoTracking()
            .Where(x =>
                x.HouseholdId == householdId &&
                x.DueDateUtc >= fromUtc &&
                x.DueDateUtc < toUtc &&
                x.StatusCode != HouseholdInvoiceStatuses.Cancelled)
            .ToArrayAsync(cancellationToken);

        var invoiceIds = invoices.Select(x => x.Id).ToArray();
        var invoicePaid = invoiceIds.Length == 0
            ? new Dictionary<Guid, long>()
            : await dbContext.HouseholdInvoicePayments
                .AsNoTracking()
                .Where(x => x.HouseholdId == householdId && invoiceIds.Contains(x.InvoiceId))
                .GroupBy(x => x.InvoiceId)
                .Select(x => new { InvoiceId = x.Key, PaidMinor = x.Sum(y => y.AmountMinor) })
                .ToDictionaryAsync(x => x.InvoiceId, x => x.PaidMinor, cancellationToken);

        var monthly = BuildMonthSkeleton(fromUtc, months)
            .Select(x =>
            {
                var items = entries.Where(t =>
                    t.OccurredAtUtc.Year == x.Year && t.OccurredAtUtc.Month == x.Month);
                var income = SumPositive(items.Select(t => t.AmountMinor));
                var expense = SumNegative(items.Select(t => t.AmountMinor));
                var plan = invoices
                    .Where(i => i.DueDateUtc.Year == x.Year && i.DueDateUtc.Month == x.Month)
                    .Sum(i => HouseholdFinanceMoney.FromMinorUnits(i.GrossAmountMinor));
                return x with
                {
                    Income = income,
                    Expense = expense,
                    Net = income - expense,
                    PlannedExpense = plan,
                    ActualExpense = expense
                };
            })
            .ToArray();

        var expenseRows = entries.Where(x => x.AmountMinor < 0).ToArray();
        var categories = BuildCategories(
            expenseRows
                .GroupBy(x => x.CategoryCode ?? string.Empty)
                .Select(g => new CategoryAmount(
                    g.Key,
                    HouseholdFinanceCategories.GetNamePl(g.Key),
                    g.Sum(x => HouseholdFinanceMoney.FromMinorUnits(-x.AmountMinor)))));

        var accountSummaries = accounts.Select(x =>
            new FinanceReportAccount(
                x.Id,
                x.Name,
                HouseholdAccountTypes.GetNamePl(x.AccountTypeCode),
                x.CurrencyCode,
                HouseholdFinanceMoney.FromMinorUnits(balances.GetValueOrDefault(x.Id)),
                x.IsActive))
            .ToArray();

        var top = expenseRows
            .OrderBy(x => x.AmountMinor)
            .Take(12)
            .Select(x => new FinanceReportTopExpense(
                x.OccurredAtUtc,
                FirstNonEmpty(x.Description, HouseholdFinanceCategories.GetNamePl(x.CategoryCode)),
                HouseholdFinanceCategories.GetNamePl(x.CategoryCode),
                HouseholdFinanceMoney.FromMinorUnits(-x.AmountMinor),
                "HouseholdEntry",
                x.Id,
                null))
            .ToArray();

        var obligations = invoices
            .Select(x =>
            {
                var paidMinor = invoicePaid.GetValueOrDefault(x.Id);
                var remainingMinor = Math.Max(0, x.GrossAmountMinor - paidMinor);
                return new FinanceReportObligation(
                    "HouseholdInvoice",
                    $"{x.Supplier} · {x.InvoiceNumber}",
                    HouseholdInvoiceCategories.GetNamePl(x.CategoryCode),
                    x.DueDateUtc,
                    HouseholdFinanceMoney.FromMinorUnits(x.GrossAmountMinor),
                    HouseholdFinanceMoney.FromMinorUnits(paidMinor),
                    HouseholdFinanceMoney.FromMinorUnits(remainingMinor),
                    x.StatusCode,
                    HouseholdInvoiceStatuses.GetNamePl(x.StatusCode),
                    x.Id,
                    null,
                    null);
            })
            .Where(x => x.RemainingAmount > 0m)
            .OrderBy(x => x.DueDateUtc)
            .Take(30)
            .ToArray();

        return new ScopeData(
            accountSummaries.Sum(x => x.Balance),
            monthly,
            categories,
            accountSummaries,
            top,
            obligations,
            []);
    }

    private async Task<ScopeData> BuildFamilyAsync(
        Guid familyGroupId,
        Guid actorPersonId,
        string currency,
        DateTime fromUtc,
        DateTime toUtc,
        int months,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(currency, "PLN", StringComparison.OrdinalIgnoreCase))
        {
            currency = "PLN";
        }

        var monthMap = BuildMonthSkeleton(fromUtc, months)
            .ToDictionary(x => $"{x.Year:0000}-{x.Month:00}");
        var fromKey = $"{fromUtc.Year:0000}-{fromUtc.Month:00}";
        var lastMonth = toUtc.AddMonths(-1);
        var toKey = $"{lastMonth.Year:0000}-{lastMonth.Month:00}";

        var accountList = new List<FinanceReportAccount>();
        var categoryAmounts = new List<CategoryAmount>();
        var topExpenses = new List<FinanceReportTopExpense>();
        var obligations = new List<FinanceReportObligation>();
        var areas = new List<FinanceReportArea>();

        await WithConnectionAsync(
            async connection =>
            {
                await ReadFamilyMonthlyAsync(
                    connection,
                    familyGroupId,
                    fromKey,
                    toKey,
                    monthMap,
                    cancellationToken);
                await ReadFamilyIncomeAsync(
                    connection,
                    familyGroupId,
                    fromKey,
                    toKey,
                    monthMap,
                    cancellationToken);
                accountList.AddRange(await ReadFamilyAccountsAsync(
                    connection,
                    familyGroupId,
                    actorPersonId,
                    currency,
                    cancellationToken));
                categoryAmounts.AddRange(await ReadFamilyCategoriesAsync(
                    connection,
                    familyGroupId,
                    fromKey,
                    toKey,
                    cancellationToken));
                topExpenses.AddRange(await ReadFamilyTopExpensesAsync(
                    connection,
                    familyGroupId,
                    fromKey,
                    toKey,
                    cancellationToken));
                obligations.AddRange(await ReadFamilyObligationsAsync(
                    connection,
                    familyGroupId,
                    fromKey,
                    toKey,
                    cancellationToken));
                areas.AddRange(await ReadFamilyAreasAsync(
                    connection,
                    familyGroupId,
                    fromKey,
                    toKey,
                    cancellationToken));
            },
            cancellationToken);

        var monthly = monthMap.Values
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Select(x => x with { Net = x.Income - x.Expense })
            .ToArray();

        return new ScopeData(
            accountList.Sum(x => x.Balance),
            monthly,
            BuildCategories(categoryAmounts),
            accountList,
            topExpenses.OrderByDescending(x => x.Amount).Take(12).ToArray(),
            obligations.OrderBy(x => x.DueDateUtc).Take(30).ToArray(),
            areas.OrderByDescending(x => x.PlannedAmount).ThenBy(x => x.Name).ToArray());
    }

    private async Task ReadFamilyMonthlyAsync(
        DbConnection connection,
        Guid familyGroupId,
        string fromKey,
        string toKey,
        IDictionary<string, FinanceReportMonth> monthMap,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT o.PeriodKey,
                   COALESCE(SUM(CASE WHEN o.StatusCode <> 'Cancelled' THEN o.PlannedAmountMinor ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN p.Id IS NOT NULL THEN p.AmountMinor ELSE 0 END), 0)
            FROM FamilyRecurringOccurrences o
            INNER JOIN FamilyRecurringRules r ON r.Id = o.RuleId
            LEFT JOIN FamilyExpensePayments p ON p.OccurrenceId = o.Id
            WHERE o.FamilyGroupId = $familyGroupId
              AND r.RuleTypeCode = 'Expense'
              AND o.PeriodKey >= $fromKey
              AND o.PeriodKey <= $toKey
            GROUP BY o.PeriodKey;
            """;
        AddParameter(command, "$familyGroupId", familyGroupId);
        AddParameter(command, "$fromKey", fromKey);
        AddParameter(command, "$toKey", toKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            if (!monthMap.TryGetValue(key, out var month))
            {
                continue;
            }
            var planned = FamilyFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(1)));
            var actual = FamilyFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(2)));
            monthMap[key] = month with
            {
                Expense = actual,
                PlannedExpense = planned,
                ActualExpense = actual
            };
        }
    }

    private async Task ReadFamilyIncomeAsync(
        DbConnection connection,
        Guid familyGroupId,
        string fromKey,
        string toKey,
        IDictionary<string, FinanceReportMonth> monthMap,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT PeriodKey, COALESCE(SUM(AmountMinor), 0)
            FROM (
                SELECT substr(t.OccurredAtUtc, 1, 7) AS PeriodKey,
                       t.AmountMinor AS AmountMinor
                FROM FamilyMembers m
                INNER JOIN FamilySharingPolicies sharing
                    ON sharing.FamilyGroupId = m.FamilyGroupId
                   AND sharing.PersonId = m.PersonId
                   AND sharing.EffectiveToUtc IS NULL
                   AND sharing.ShareActualIncome = 1
                INNER JOIN PersonalFinancialTransactions t
                    ON t.OwnerPersonId = m.PersonId
                LEFT JOIN PersonalFinancialTransactions source
                    ON source.Id = t.CorrectsTransactionId
                WHERE m.FamilyGroupId = $familyGroupId
                  AND m.FamilyRoleCode = 'Adult'
                  AND m.ValidToUtc IS NULL
                  AND substr(t.OccurredAtUtc, 1, 7) >= $fromKey
                  AND substr(t.OccurredAtUtc, 1, 7) <= $toKey
                  AND (
                        t.KindCode = 'Income'
                        OR (t.KindCode = 'Correction' AND source.KindCode = 'Income')
                      )
                  AND NOT EXISTS (
                        SELECT 1
                        FROM FamilySharedAccounts sharedAccount
                        WHERE sharedAccount.FamilyGroupId = $familyGroupId
                          AND sharedAccount.PersonalAccountId = t.AccountId
                          AND sharedAccount.ClosedAtUtc IS NULL
                      )
                  AND NOT EXISTS (
                        SELECT 1
                        FROM FamilyIncomeReceipts childReceipt
                        WHERE childReceipt.FamilyGroupId = $familyGroupId
                          AND childReceipt.SourceType = 'PersonalTransaction'
                          AND (childReceipt.SourceId = t.Id OR childReceipt.SourceId = t.CorrectsTransactionId)
                      )

                UNION ALL

                SELECT substr(receipt.ReceivedAtUtc, 1, 7) AS PeriodKey,
                       receipt.AmountMinor AS AmountMinor
                FROM FamilyIncomeReceipts receipt
                WHERE receipt.FamilyGroupId = $familyGroupId
                  AND substr(receipt.ReceivedAtUtc, 1, 7) >= $fromKey
                  AND substr(receipt.ReceivedAtUtc, 1, 7) <= $toKey
            ) incomeRows
            GROUP BY PeriodKey;
            """;
        AddParameter(command, "$familyGroupId", familyGroupId);
        AddParameter(command, "$fromKey", fromKey);
        AddParameter(command, "$toKey", toKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            if (!monthMap.TryGetValue(key, out var month))
            {
                continue;
            }
            monthMap[key] = month with
            {
                Income = FamilyFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(1)))
            };
        }
    }

    private async Task<IReadOnlyList<FinanceReportAccount>> ReadFamilyAccountsAsync(
        DbConnection connection,
        Guid familyGroupId,
        Guid actorPersonId,
        string currency,
        CancellationToken cancellationToken)
    {
        var result = new List<FinanceReportAccount>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT a.Id, a.Name, a.AccountTypeCode, a.CurrencyCode, a.IsActive,
                   COALESCE(SUM(t.AmountMinor), 0)
            FROM FamilySharedAccounts s
            INNER JOIN PersonalFinancialAccounts a ON a.Id = s.PersonalAccountId
            LEFT JOIN PersonalFinancialTransactions t ON t.AccountId = a.Id
            WHERE s.FamilyGroupId = $familyGroupId
              AND s.ClosedAtUtc IS NULL
              AND (s.OwnerPersonId = $actorPersonId OR s.CoOwnerPersonId = $actorPersonId)
              AND a.CurrencyCode = $currency
            GROUP BY a.Id, a.Name, a.AccountTypeCode, a.CurrencyCode, a.IsActive
            ORDER BY a.Name;
            """;
        AddParameter(command, "$familyGroupId", familyGroupId);
        AddParameter(command, "$actorPersonId", actorPersonId);
        AddParameter(command, "$currency", currency);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new FinanceReportAccount(
                reader.GetGuid(0),
                reader.GetString(1),
                $"Wspólne · {PersonalAccountTypes.GetNamePl(reader.GetString(2))}",
                reader.GetString(3),
                PersonalFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(5))),
                Convert.ToInt64(reader.GetValue(4)) != 0));
        }
        return result;
    }

    private async Task<IReadOnlyList<CategoryAmount>> ReadFamilyCategoriesAsync(
        DbConnection connection,
        Guid familyGroupId,
        string fromKey,
        string toKey,
        CancellationToken cancellationToken)
    {
        var result = new List<CategoryAmount>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT r.CategoryCode, COALESCE(SUM(p.AmountMinor), 0)
            FROM FamilyExpensePayments p
            INNER JOIN FamilyRecurringOccurrences o ON o.Id = p.OccurrenceId
            INNER JOIN FamilyRecurringRules r ON r.Id = o.RuleId
            WHERE p.FamilyGroupId = $familyGroupId
              AND o.PeriodKey >= $fromKey
              AND o.PeriodKey <= $toKey
            GROUP BY r.CategoryCode;
            """;
        AddParameter(command, "$familyGroupId", familyGroupId);
        AddParameter(command, "$fromKey", fromKey);
        AddParameter(command, "$toKey", toKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(0);
            result.Add(new CategoryAmount(
                code,
                FamilyBudgetCategories.GetNamePl(code),
                FamilyFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(1)))));
        }
        return result;
    }

    private async Task<IReadOnlyList<FinanceReportTopExpense>> ReadFamilyTopExpensesAsync(
        DbConnection connection,
        Guid familyGroupId,
        string fromKey,
        string toKey,
        CancellationToken cancellationToken)
    {
        var result = new List<FinanceReportTopExpense>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT p.Id, p.PaidAtUtc, r.Name, r.CategoryCode, p.AmountMinor, r.AreaId
            FROM FamilyExpensePayments p
            INNER JOIN FamilyRecurringOccurrences o ON o.Id = p.OccurrenceId
            INNER JOIN FamilyRecurringRules r ON r.Id = o.RuleId
            WHERE p.FamilyGroupId = $familyGroupId
              AND o.PeriodKey >= $fromKey
              AND o.PeriodKey <= $toKey
            ORDER BY p.AmountMinor DESC
            LIMIT 12;
            """;
        AddParameter(command, "$familyGroupId", familyGroupId);
        AddParameter(command, "$fromKey", fromKey);
        AddParameter(command, "$toKey", toKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new FinanceReportTopExpense(
                ReadDateTime(reader, 1),
                reader.GetString(2),
                FamilyBudgetCategories.GetNamePl(reader.GetString(3)),
                FamilyFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(4))),
                "FamilyExpensePayment",
                reader.GetGuid(0),
                reader.IsDBNull(5) ? null : reader.GetGuid(5)));
        }
        return result;
    }

    private async Task<IReadOnlyList<FinanceReportObligation>> ReadFamilyObligationsAsync(
        DbConnection connection,
        Guid familyGroupId,
        string fromKey,
        string toKey,
        CancellationToken cancellationToken)
    {
        var result = new List<FinanceReportObligation>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT o.Id, r.Name, a.Name, o.PlannedDateUtc, o.PlannedAmountMinor,
                   COALESCE(p.AmountMinor, 0), o.StatusCode, r.AreaId
            FROM FamilyRecurringOccurrences o
            INNER JOIN FamilyRecurringRules r ON r.Id = o.RuleId
            LEFT JOIN FamilyBudgetAreas a ON a.Id = r.AreaId
            LEFT JOIN FamilyExpensePayments p ON p.OccurrenceId = o.Id
            WHERE o.FamilyGroupId = $familyGroupId
              AND r.RuleTypeCode = 'Expense'
              AND o.PeriodKey >= $fromKey
              AND o.PeriodKey <= $toKey
              AND o.StatusCode <> 'Cancelled'
            ORDER BY o.PlannedDateUtc;
            """;
        AddParameter(command, "$familyGroupId", familyGroupId);
        AddParameter(command, "$fromKey", fromKey);
        AddParameter(command, "$toKey", toKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var amountMinor = Convert.ToInt64(reader.GetValue(4));
            var paidMinor = Convert.ToInt64(reader.GetValue(5));
            var status = reader.GetString(6);
            result.Add(new FinanceReportObligation(
                "FamilyExpense",
                reader.GetString(1),
                reader.IsDBNull(2) ? "Finanse rodzinne" : reader.GetString(2),
                ReadDateTime(reader, 3),
                FamilyFinanceMoney.FromMinorUnits(amountMinor),
                FamilyFinanceMoney.FromMinorUnits(paidMinor),
                FamilyFinanceMoney.FromMinorUnits(Math.Max(0, amountMinor - paidMinor)),
                status,
                FamilyRecurringOccurrenceStatuses.GetNamePl(status),
                reader.GetGuid(0),
                familyGroupId,
                reader.IsDBNull(7) ? null : reader.GetGuid(7)));
        }
        return result;
    }

    private async Task<IReadOnlyList<FinanceReportArea>> ReadFamilyAreasAsync(
        DbConnection connection,
        Guid familyGroupId,
        string fromKey,
        string toKey,
        CancellationToken cancellationToken)
    {
        var result = new List<FinanceReportArea>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT a.Id, a.Name,
                   COALESCE(SUM(CASE WHEN o.StatusCode <> 'Cancelled' THEN o.PlannedAmountMinor ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN p.Id IS NOT NULL THEN p.AmountMinor ELSE 0 END), 0),
                   COUNT(DISTINCT r.Id)
            FROM FamilyBudgetAreas a
            LEFT JOIN FamilyRecurringRules r
              ON r.AreaId = a.Id AND r.RuleTypeCode = 'Expense'
            LEFT JOIN FamilyRecurringOccurrences o
              ON o.RuleId = r.Id AND o.PeriodKey >= $fromKey AND o.PeriodKey <= $toKey
            LEFT JOIN FamilyExpensePayments p ON p.OccurrenceId = o.Id
            WHERE a.FamilyGroupId = $familyGroupId
              AND a.IsActive = 1
              AND (a.SystemCode IS NULL OR a.SystemCode <> 'HouseholdContributions')
            GROUP BY a.Id, a.Name
            ORDER BY a.SortOrder, a.Name;
            """;
        AddParameter(command, "$familyGroupId", familyGroupId);
        AddParameter(command, "$fromKey", fromKey);
        AddParameter(command, "$toKey", toKey);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var planned = FamilyFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(2)));
            var actual = FamilyFinanceMoney.FromMinorUnits(Convert.ToInt64(reader.GetValue(3)));
            result.Add(new FinanceReportArea(
                reader.GetGuid(0),
                reader.GetString(1),
                planned,
                actual,
                Math.Max(0m, planned - actual),
                Convert.ToInt32(reader.GetValue(4))));
        }
        return result;
    }

    private async Task<IReadOnlyList<FinanceReportFamilyOption>> GetFamilyOptionsAsync(
        Guid personId,
        CancellationToken cancellationToken)
    {
        var result = new List<FinanceReportFamilyOption>();
        await WithConnectionAsync(
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT g.Id, g.Name
                    FROM FamilyGroups g
                    INNER JOIN FamilyMembers m ON m.FamilyGroupId = g.Id
                    WHERE g.IsActive = 1
                      AND m.PersonId = $personId
                      AND m.ValidToUtc IS NULL
                    ORDER BY g.Name;
                    """;
                AddParameter(command, "$personId", personId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    result.Add(new FinanceReportFamilyOption(
                        reader.GetGuid(0),
                        reader.GetString(1)));
                }
            },
            cancellationToken);
        return result;
    }

    private async Task<IReadOnlyList<string>> GetPersonalCurrenciesAsync(
        Guid personId,
        CancellationToken cancellationToken) =>
        await dbContext.PersonalFinancialAccounts
            .AsNoTracking()
            .Where(x => x.OwnerPersonId == personId)
            .Select(x => x.CurrencyCode)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyList<string>> GetHouseholdCurrenciesAsync(
        Guid householdId,
        CancellationToken cancellationToken) =>
        await dbContext.HouseholdAccounts
            .AsNoTracking()
            .Where(x => x.HouseholdId == householdId)
            .Select(x => x.CurrencyCode)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(cancellationToken);

    private async Task<Guid> GetActorPersonIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var personId = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(x => x.Id == actorUserId && x.IsActive)
            .Select(x => x.PersonId)
            .SingleOrDefaultAsync(cancellationToken);

        if (personId == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                "Nie można ustalić osoby powiązanej z kontem użytkownika.");
        }

        return personId;
    }

    private static IReadOnlyList<FinanceReportMonth> BuildMonthSkeleton(
        DateTime fromUtc,
        int months)
    {
        var culture = CultureInfo.GetCultureInfo("pl-PL");
        return Enumerable.Range(0, months)
            .Select(offset => fromUtc.AddMonths(offset))
            .Select(date => new FinanceReportMonth(
                date.Year,
                date.Month,
                date.ToString("MMM yyyy", culture),
                0m,
                0m,
                0m,
                0m,
                0m))
            .ToArray();
    }

    private static IReadOnlyList<FinanceReportCategory> BuildCategories(
        IEnumerable<CategoryAmount> categories)
    {
        var rows = categories
            .Where(x => x.Amount > 0m)
            .OrderByDescending(x => x.Amount)
            .ToArray();
        var total = rows.Sum(x => x.Amount);
        return rows.Select(x => new FinanceReportCategory(
                x.Code,
                x.Name,
                x.Amount,
                total == 0m ? 0m : decimal.Round(x.Amount / total * 100m, 1)))
            .ToArray();
    }

    private static decimal SumPositive(IEnumerable<long> values) =>
        values.Where(x => x > 0)
            .Sum(x => x / 100m);

    private static decimal SumNegative(IEnumerable<long> values) =>
        values.Where(x => x < 0)
            .Sum(x => -x / 100m);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim()
        ?? "Wydatek";

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency)
            ? "PLN"
            : currency.Trim().ToUpperInvariant();

    private static void ValidateSimulation(FinanceSimulationRequest request)
    {
        if (!FinanceReportScopes.IsValid(request.ScopeCode))
        {
            throw new ArgumentException("Wybierz poprawny zakres symulacji.");
        }
        if (request.HistoryMonths < 1 || request.HistoryMonths > 36)
        {
            throw new ArgumentOutOfRangeException(nameof(request.HistoryMonths),
                "Okres bazowy musi mieścić się w zakresie 1-36 miesięcy.");
        }
        if (request.HorizonMonths < 3 || request.HorizonMonths > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(request.HorizonMonths),
                "Horyzont symulacji musi mieścić się w zakresie 3-60 miesięcy.");
        }
        if (request.OneTimeMonth < 1 || request.OneTimeMonth > request.HorizonMonths)
        {
            throw new ArgumentOutOfRangeException(nameof(request.OneTimeMonth),
                "Miesiąc zdarzenia jednorazowego musi mieścić się w horyzoncie symulacji.");
        }
        ValidatePercent(request.IncomeChangePercent, nameof(request.IncomeChangePercent));
        ValidatePercent(request.ExpenseChangePercent, nameof(request.ExpenseChangePercent));
        ValidatePercent(request.AnnualIncomeGrowthPercent, nameof(request.AnnualIncomeGrowthPercent));
        ValidatePercent(request.AnnualExpenseInflationPercent, nameof(request.AnnualExpenseInflationPercent));
        if (request.ExtraMonthlyIncome < 0m || request.ExtraMonthlyExpense < 0m ||
            request.OneTimeIncome < 0m || request.OneTimeExpense < 0m || request.TargetReserve < 0m)
        {
            throw new ArgumentException("Kwoty w symulacji nie mogą być ujemne.");
        }
    }

    private static void ValidatePercent(decimal value, string name)
    {
        if (value < -100m || value > 500m)
        {
            throw new ArgumentOutOfRangeException(name,
                "Zmiana procentowa musi mieścić się w zakresie od -100% do 500%.");
        }
    }

    private static decimal CompoundFactor(decimal annualPercent, int elapsedMonths)
    {
        if (annualPercent == 0m || elapsedMonths == 0)
        {
            return 1m;
        }
        var annual = 1d + (double)(annualPercent / 100m);
        if (annual <= 0d)
        {
            return 0m;
        }
        return (decimal)Math.Pow(annual, elapsedMonths / 12d);
    }

    private async Task WithConnectionAsync(
        Func<DbConnection, Task> action,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await action(connection);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static DateTime ReadDateTime(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        if (value is DateTime dateTime)
        {
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
        return DateTime.SpecifyKind(
            DateTime.Parse(
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind),
            DateTimeKind.Utc);
    }

    private sealed record CategoryAmount(
        string Code,
        string Name,
        decimal Amount);

    private sealed record ScopeData(
        decimal CurrentBalance,
        IReadOnlyList<FinanceReportMonth> Monthly,
        IReadOnlyList<FinanceReportCategory> Categories,
        IReadOnlyList<FinanceReportAccount> Accounts,
        IReadOnlyList<FinanceReportTopExpense> TopExpenses,
        IReadOnlyList<FinanceReportObligation> Obligations,
        IReadOnlyList<FinanceReportArea> Areas);
}
