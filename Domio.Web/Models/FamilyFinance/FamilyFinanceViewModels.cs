using System.ComponentModel.DataAnnotations;
using Domio.Application.FamilyFinance;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.FamilyFinance;

public sealed class FamilyFinanceIndexViewModel
{
    public required FamilyFinanceOverview Overview { get; init; }

    public FamilyBudgetOverview? Budget { get; init; }

    public string SelectedTab { get; init; } = "overview";
}

public sealed class CreateFamilyGroupViewModel
{
    [Required(ErrorMessage = "Podaj nazwę rodziny.")]
    [MaxLength(160)]
    [Display(Name = "Nazwa rodziny")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AddFamilyMemberViewModel
{
    public Guid FamilyGroupId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz osobę.")]
    [Display(Name = "Osoba")]
    public Guid PersonId { get; set; }

    [Required(ErrorMessage = "Wybierz rolę w rodzinie.")]
    [Display(Name = "Rola w rodzinie")]
    public string FamilyRoleCode { get; set; } = string.Empty;

    public List<SelectListItem> People { get; set; } = [];

    public List<SelectListItem> Roles { get; set; } = [];
}

public sealed class FamilySharingViewModel
{
    public Guid FamilyGroupId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    public string PersonDisplayName { get; set; } = string.Empty;

    [Display(Name = "Planowane przychody")]
    public bool SharePlannedIncome { get; set; }

    [Display(Name = "Rzeczywiste przychody")]
    public bool ShareActualIncome { get; set; }

    [Display(Name = "Wydatki rodzinne")]
    public bool ShareFamilyExpenses { get; set; }

    [Display(Name = "Reguły cykliczne")]
    public bool ShareRecurringRules { get; set; }
}

public sealed class CreateFamilyExpenseViewModel
{
    public Guid FamilyGroupId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwę kosztu.")]
    [MaxLength(160)]
    [Display(Name = "Nazwa kosztu")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz kategorię.")]
    [Display(Name = "Kategoria")]
    public string CategoryCode { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Planowana kwota")]
    public decimal PlannedAmount { get; set; }

    [Required(ErrorMessage = "Wybierz częstotliwość.")]
    [Display(Name = "Częstotliwość")]
    public string FrequencyCode { get; set; } = string.Empty;

    [Range(1, 31, ErrorMessage = "Dzień terminu musi mieścić się w zakresie 1-31.")]
    [Display(Name = "Dzień miesiąca")]
    public int DueDay { get; set; } = 1;

    [Display(Name = "Koszt przypisany do osoby")]
    public Guid? BeneficiaryPersonId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Od kiedy")]
    public DateTime ActiveFromUtc { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "Do kiedy (opcjonalnie)")]
    public DateTime? ActiveToUtc { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];
    public List<SelectListItem> Frequencies { get; set; } = [];
    public List<SelectListItem> Beneficiaries { get; set; } = [];
}

public sealed class FamilyBudgetLinksViewModel
{
    public required FamilyBudgetLinkForm Form { get; init; }

    [Required(ErrorMessage = "Wybierz źródło.")]
    [Display(Name = "Źródło")]
    public string SelectedSourceKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz kategorię rodzinną.")]
    [Display(Name = "Kategoria rodzinna")]
    public string CategoryCode { get; set; } = string.Empty;

    [Display(Name = "Koszt przypisany do osoby")]
    public Guid? BeneficiaryPersonId { get; set; }

    public List<SelectListItem> Sources { get; set; } = [];
    public List<SelectListItem> Categories { get; set; } = [];
    public List<SelectListItem> Beneficiaries { get; set; } = [];
}

public sealed class PayFamilyExpenseViewModel
{
    public Guid FamilyGroupId { get; set; }

    public Guid OccurrenceId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string CategoryNamePl { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime PlannedDateUtc { get; set; }

    public string? BeneficiaryDisplayName { get; set; }

    [Required(ErrorMessage = "Wybierz konto, z którego ma zostać opłacony koszt.")]
    [Display(Name = "Zapłać z konta")]
    public string SelectedAccountKey { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Data płatności")]
    public DateTime PaidAtUtc { get; set; } = DateTime.Today;

    public List<SelectListItem> Accounts { get; set; } = [];
}

public sealed class CreateFamilyChildIncomeViewModel
{
    public Guid FamilyGroupId { get; set; }

    public Guid BeneficiaryPersonId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    public string BeneficiaryDisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz rodzaj przychodu.")]
    [Display(Name = "Rodzaj przychodu")]
    public string IncomeKindCode { get; set; } = string.Empty;

    [MaxLength(160)]
    [Display(Name = "Własna nazwa (dla opcji Inny)")]
    public string? CustomName { get; set; }

    [Range(typeof(decimal), "0.01", "999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true, ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Planowana kwota")]
    public decimal PlannedAmount { get; set; }

    [Required(ErrorMessage = "Wybierz częstotliwość.")]
    [Display(Name = "Częstotliwość")]
    public string FrequencyCode { get; set; } = string.Empty;

    [Range(1, 31, ErrorMessage = "Dzień wpływu musi mieścić się w zakresie 1-31.")]
    [Display(Name = "Dzień wpływu")]
    public int DueDay { get; set; } = 1;

    [DataType(DataType.Date)]
    [Display(Name = "Od kiedy")]
    public DateTime ActiveFromUtc { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "Do kiedy (opcjonalnie)")]
    public DateTime? ActiveToUtc { get; set; }

    public List<SelectListItem> IncomeKinds { get; set; } = [];

    public List<SelectListItem> Frequencies { get; set; } = [];
}

public sealed class ConfirmFamilyChildIncomeReceiptViewModel
{
    public Guid FamilyGroupId { get; set; }

    public Guid RuleId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    public string BeneficiaryDisplayName { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string IncomeKindNamePl { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime PlannedDateUtc { get; set; }

    [Required(ErrorMessage = "Wybierz konto, na które wpłynęły pieniądze.")]
    [Display(Name = "Wpływ na konto")]
    public string SelectedAccountKey { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Data wpływu")]
    public DateTime ReceivedAtUtc { get; set; } = DateTime.Today;

    public List<SelectListItem> Accounts { get; set; } = [];
}

public sealed class CreateFamilySharedAccountViewModel
{
    public Guid FamilyGroupId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwę wspólnego konta.")]
    [MaxLength(120)]
    [Display(Name = "Nazwa konta")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz typ konta.")]
    [Display(Name = "Typ konta")]
    public string AccountTypeCode { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0",
        "999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Saldo początkowe nie może być ujemne.")]
    [Display(Name = "Saldo początkowe")]
    public decimal InitialBalance { get; set; }

    [Required(ErrorMessage = "Wybierz właściciela.")]
    [Display(Name = "Właściciel")]
    public Guid OwnerPersonId { get; set; }

    [Required(ErrorMessage = "Wybierz współwłaściciela.")]
    [Display(Name = "Współwłaściciel")]
    public Guid CoOwnerPersonId { get; set; }

    public List<SelectListItem> AccountTypes { get; set; } = [];

    public List<SelectListItem> Adults { get; set; } = [];
}

public sealed class FamilySharedAccountOperationViewModel
{
    public Guid SharedAccountId { get; set; }

    public Guid FamilyGroupId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    public decimal Balance { get; set; }

    public string CurrentPersonRoleNamePl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz rodzaj operacji.")]
    [Display(Name = "Rodzaj operacji")]
    public string KindCode { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Kwota")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data operacji")]
    public DateTime OccurredAtUtc { get; set; } = DateTime.Today;

    [Display(Name = "Kategoria")]
    public string CategoryCode { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Kontrahent / płatnik")]
    public string? Counterparty { get; set; }

    [MaxLength(500)]
    [Display(Name = "Opis")]
    public string? Description { get; set; }

    public List<SelectListItem> OperationKinds { get; set; } = [];

    public List<SelectListItem> Categories { get; set; } = [];
}

public sealed class PayFamilyChildContributionViewModel
{
    public Guid FamilyGroupId { get; set; }

    public Guid ObligationId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    public string ChildDisplayName { get; set; } = string.Empty;

    public string PeriodKey { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Amount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public DateTime DueDateUtc { get; set; }

    public string TargetHouseholdAccountName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    [Required(ErrorMessage = "Wybierz konto, z którego ma zostać pokryta składka dziecka.")]
    [Display(Name = "Źródło środków")]
    public string SelectedSourceKey { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Data przekazania")]
    public DateTime PaidAtUtc { get; set; } = DateTime.Today;

    public List<SelectListItem> Sources { get; set; } = [];
}

