namespace Domio.Application.Reporting;

public static class FinanceReportScopes
{
    public const string Personal = "Personal";
    public const string Household = "Household";
    public const string Family = "Family";

    public static readonly IReadOnlyList<string> All =
    [
        Personal,
        Household,
        Family
    ];

    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Contains(code);

    public static string GetNamePl(string code) =>
        code switch
        {
            Personal => "Finanse osobiste",
            Household => "Finanse domu",
            Family => "Finanse rodzinne",
            _ => code
        };
}

public sealed record FinanceReportQuery(
    string? ScopeCode,
    int Months,
    string? CurrencyCode,
    Guid? FamilyGroupId);

public sealed record FinanceReportScopeOption(
    string ScopeCode,
    string NamePl,
    bool IsAvailable);

public sealed record FinanceReportFamilyOption(
    Guid FamilyGroupId,
    string Name);

public sealed record FinanceReportKpis(
    decimal CurrentBalance,
    decimal Income,
    decimal Expense,
    decimal Net,
    decimal AverageMonthlyIncome,
    decimal AverageMonthlyExpense,
    decimal SavingsRatePercent,
    decimal? RunwayMonths,
    decimal LargestExpense,
    string? LargestExpenseLabel);

public sealed record FinanceReportMonth(
    int Year,
    int Month,
    string Label,
    decimal Income,
    decimal Expense,
    decimal Net,
    decimal PlannedExpense,
    decimal ActualExpense);

public sealed record FinanceReportCategory(
    string CategoryCode,
    string CategoryNamePl,
    decimal Amount,
    decimal Percent);

public sealed record FinanceReportAccount(
    Guid AccountId,
    string Name,
    string AccountTypeNamePl,
    string CurrencyCode,
    decimal Balance,
    bool IsActive);

public sealed record FinanceReportTopExpense(
    DateTime DateUtc,
    string Label,
    string CategoryNamePl,
    decimal Amount,
    string SourceKind,
    Guid SourceId,
    Guid? AreaId);

public sealed record FinanceReportObligation(
    string SourceKind,
    string Label,
    string? ContextLabel,
    DateTime DueDateUtc,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string StatusCode,
    string StatusNamePl,
    Guid SourceId,
    Guid? FamilyGroupId,
    Guid? AreaId);

public sealed record FinanceReportArea(
    Guid AreaId,
    string Name,
    decimal PlannedAmount,
    decimal ActualAmount,
    decimal RemainingAmount,
    int ItemsCount);

public sealed record FinanceReportDashboard(
    string ScopeCode,
    string ScopeNamePl,
    string CurrencyCode,
    DateTime FromUtc,
    DateTime ToUtc,
    int Months,
    Guid? FamilyGroupId,
    string? FamilyGroupName,
    IReadOnlyList<FinanceReportScopeOption> ScopeOptions,
    IReadOnlyList<FinanceReportFamilyOption> FamilyOptions,
    IReadOnlyList<string> AvailableCurrencies,
    FinanceReportKpis Kpis,
    IReadOnlyList<FinanceReportMonth> Monthly,
    IReadOnlyList<FinanceReportCategory> Categories,
    IReadOnlyList<FinanceReportAccount> Accounts,
    IReadOnlyList<FinanceReportTopExpense> TopExpenses,
    IReadOnlyList<FinanceReportObligation> Obligations,
    IReadOnlyList<FinanceReportArea> Areas);

public sealed record FinanceSimulationRequest(
    string ScopeCode,
    int HistoryMonths,
    string CurrencyCode,
    Guid? FamilyGroupId,
    int HorizonMonths,
    decimal IncomeChangePercent,
    decimal ExpenseChangePercent,
    decimal ExtraMonthlyIncome,
    decimal ExtraMonthlyExpense,
    decimal OneTimeIncome,
    decimal OneTimeExpense,
    int OneTimeMonth,
    decimal AnnualIncomeGrowthPercent,
    decimal AnnualExpenseInflationPercent,
    decimal TargetReserve);

public sealed record FinanceSimulationMonth(
    int MonthNumber,
    string Label,
    decimal BaselineIncome,
    decimal BaselineExpense,
    decimal BaselineBalance,
    decimal ScenarioIncome,
    decimal ScenarioExpense,
    decimal ScenarioBalance);

public sealed record FinanceSimulationResult(
    decimal StartingBalance,
    decimal BaselineMonthlyIncome,
    decimal BaselineMonthlyExpense,
    decimal BaselineEndBalance,
    decimal ScenarioEndBalance,
    decimal Difference,
    decimal LowestScenarioBalance,
    int? FirstNegativeMonth,
    int? TargetReserveReachedMonth,
    decimal TargetReserve,
    IReadOnlyList<FinanceSimulationMonth> Months);
