using System.ComponentModel.DataAnnotations;
using Domio.Application.HouseholdFinance;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.HouseholdFinance;

public sealed class HouseholdContributionAdminIndexViewModel
{
    public required HouseholdContributionAdminOverview Overview { get; init; }
}

public sealed class EditHouseholdContributionRuleViewModel
{
    [Required]
    public Guid RuleId { get; set; }

    public string HouseholdMemberName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz sposób naliczania.")]
    [Display(Name = "Sposób naliczania")]
    public string ModeCode { get; set; } = string.Empty;

    [Display(Name = "Stała kwota")]
    public decimal? FixedAmount { get; set; }

    [Display(Name = "Procent planowanego wynagrodzenia")]
    public decimal? Percentage { get; set; }

    [Range(0, 31, ErrorMessage = "Termin musi mieścić się w zakresie 0-31 dni.")]
    [Display(Name = "Termin po planowanej wypłacie (dni)")]
    public int DueOffsetDays { get; set; }

    [Required(ErrorMessage = "Wybierz konto docelowe.")]
    [Display(Name = "Konto docelowe domu")]
    public Guid TargetHouseholdAccountId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Obowiązuje do")]
    public DateTime? ValidTo { get; set; }

    [Range(0, 31, ErrorMessage = "Przypomnienie musi mieścić się w zakresie 0-31 dni.")]
    [Display(Name = "Przypomnienie przed terminem (dni)")]
    public int ReminderDays { get; set; }

    public DateTime CurrentValidFrom { get; set; }

    public string? IncomeRuleName { get; set; }

    public decimal? PlannedIncomeAmount { get; set; }

    public string CurrencyCode { get; set; } = "PLN";

    public bool IsChildContribution { get; set; }

    public List<SelectListItem> Modes { get; set; } = [];

    public List<SelectListItem> TargetAccounts { get; set; } = [];
}

public sealed class CreateHouseholdChildViewModel
{
    [Required(ErrorMessage = "Podaj imię dziecka.")]
    [MaxLength(100)]
    [Display(Name = "Imię")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwisko dziecka.")]
    [MaxLength(100)]
    [Display(Name = "Nazwisko")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Nazwa wyświetlana (opcjonalnie)")]
    public string? DisplayName { get; set; }
}

public sealed class CreateHouseholdChildContributionViewModel
{
    [Required]
    public Guid HouseholdMemberId { get; set; }

    public string ChildName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    [Required(ErrorMessage = "Podaj kwotę składki.")]
    [Range(0.01d, 999999999999.99d, ErrorMessage = "Kwota składki musi być większa od zera.")]
    [Display(Name = "Miesięczna składka")]
    public decimal FixedAmount { get; set; }

    [Range(1, 28, ErrorMessage = "Dzień terminu musi mieścić się w zakresie 1-28.")]
    [Display(Name = "Termin składki - dzień miesiąca")]
    public int DueDay { get; set; } = 10;

    [Required(ErrorMessage = "Wybierz konto docelowe domu.")]
    [Display(Name = "Konto docelowe domu")]
    public Guid TargetHouseholdAccountId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Obowiązuje od")]
    public DateTime ValidFrom { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "Obowiązuje do")]
    public DateTime? ValidTo { get; set; }

    [Range(0, 28, ErrorMessage = "Przypomnienie musi mieścić się w zakresie 0-28 dni.")]
    [Display(Name = "Przypomnienie przed terminem (dni)")]
    public int ReminderDays { get; set; } = 3;

    public List<SelectListItem> TargetAccounts { get; set; } = [];
}
