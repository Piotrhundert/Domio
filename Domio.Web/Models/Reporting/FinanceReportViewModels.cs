using System.ComponentModel.DataAnnotations;
using Domio.Application.Reporting;

namespace Domio.Web.Models.Reporting;

public sealed class FinanceReportPageViewModel
{
    public required FinanceReportDashboard Dashboard { get; init; }

    public FinanceSimulationViewModel Simulation { get; set; } = new();

    public FinanceSimulationResult? SimulationResult { get; init; }
}

public sealed class FinanceSimulationViewModel
{
    public string ScopeCode { get; set; } = FinanceReportScopes.Personal;

    public Guid? FamilyGroupId { get; set; }

    public string CurrencyCode { get; set; } = "PLN";

    [Range(1, 36)]
    public int HistoryMonths { get; set; } = 12;

    [Range(3, 60, ErrorMessage = "Horyzont może mieć od 3 do 60 miesięcy.")]
    [Display(Name = "Horyzont symulacji")]
    public int HorizonMonths { get; set; } = 12;

    [Range(-100d, 500d)]
    [Display(Name = "Zmiana miesięcznych przychodów (%)")]
    public decimal IncomeChangePercent { get; set; }

    [Range(-100d, 500d)]
    [Display(Name = "Zmiana miesięcznych wydatków (%)")]
    public decimal ExpenseChangePercent { get; set; }

    [Range(0d, 999999999d)]
    [Display(Name = "Dodatkowy przychód co miesiąc")]
    public decimal ExtraMonthlyIncome { get; set; }

    [Range(0d, 999999999d)]
    [Display(Name = "Dodatkowy wydatek co miesiąc")]
    public decimal ExtraMonthlyExpense { get; set; }

    [Range(0d, 999999999d)]
    [Display(Name = "Jednorazowy przychód")]
    public decimal OneTimeIncome { get; set; }

    [Range(0d, 999999999d)]
    [Display(Name = "Jednorazowy wydatek")]
    public decimal OneTimeExpense { get; set; }

    [Range(1, 60)]
    [Display(Name = "W którym miesiącu zdarzenie jednorazowe")]
    public int OneTimeMonth { get; set; } = 1;

    [Range(-100d, 500d)]
    [Display(Name = "Roczny wzrost przychodów (%)")]
    public decimal AnnualIncomeGrowthPercent { get; set; }

    [Range(-100d, 500d)]
    [Display(Name = "Roczny wzrost wydatków / inflacja (%)")]
    public decimal AnnualExpenseInflationPercent { get; set; }

    [Range(0d, 999999999d)]
    [Display(Name = "Cel rezerwy finansowej")]
    public decimal TargetReserve { get; set; }

    public FinanceSimulationRequest ToRequest() =>
        new(
            ScopeCode,
            HistoryMonths,
            CurrencyCode,
            FamilyGroupId,
            HorizonMonths,
            IncomeChangePercent,
            ExpenseChangePercent,
            ExtraMonthlyIncome,
            ExtraMonthlyExpense,
            OneTimeIncome,
            OneTimeExpense,
            OneTimeMonth,
            AnnualIncomeGrowthPercent,
            AnnualExpenseInflationPercent,
            TargetReserve);
}
