using System.ComponentModel.DataAnnotations;
using Domio.Application.HouseholdFinance;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.HouseholdFinance;

public sealed class HouseholdContributionIndexViewModel
{
    public HouseholdContributionOverview? Overview { get; set; }

    [Display(Name = "Dodaj domownika")]
    public Guid? CandidatePersonId { get; set; }

    public List<SelectListItem> CandidatePeople { get; set; } = [];
}

public sealed class CreateHouseholdContributionRuleViewModel
{
    [Required(
        ErrorMessage =
            "Wybierz rolę.")]
    [Display(Name = "Rola")]
    public int RoleDefinitionId { get; set; }

    [Required(
        ErrorMessage =
            "Wybierz sposób naliczania.")]
    [Display(Name = "Sposób naliczania")]
    public string ModeCode { get; set; } =
        string.Empty;

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage =
            "Kwota musi być większa od zera.")]
    [Display(Name = "Stała kwota dla każdej osoby")]
    public decimal? FixedAmount { get; set; }

    [Range(
        0.01d,
        100d,
        ErrorMessage =
            "Procent musi mieścić się w zakresie 0,01-100,00.")]
    [Display(Name = "Procent planowanego wynagrodzenia każdej osoby")]
    public decimal? Percentage { get; set; }

    [Range(
        0,
        31,
        ErrorMessage =
            "Przesunięcie terminu musi mieścić się w zakresie 0-31 dni.")]
    [Display(Name = "Termin po planowanej wypłacie (dni)")]
    public int DueOffsetDays { get; set; } = 7;

    [Required(
        ErrorMessage =
            "Wybierz konto docelowe gospodarstwa.")]
    [Display(Name = "Konto docelowe domu")]
    public Guid TargetHouseholdAccountId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Obowiązuje od")]
    public DateTime ValidFrom { get; set; } =
        DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "Obowiązuje do")]
    public DateTime? ValidTo { get; set; }

    [Range(
        0,
        31,
        ErrorMessage =
            "Przypomnienie musi mieścić się w zakresie 0-31 dni.")]
    [Display(Name = "Przypomnienie przed terminem (dni)")]
    public int ReminderDays { get; set; } = 3;

    public List<SelectListItem> Roles { get; set; } = [];

    public List<SelectListItem> Modes { get; set; } = [];

    public List<SelectListItem> TargetAccounts { get; set; } = [];
}
