using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.FamilyFinance;

public sealed class CreateFamilyAreaViewModel
{
    public Guid FamilyGroupId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwę obszaru.")]
    [MaxLength(100)]
    [Display(Name = "Nazwa obszaru")]
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateFamilyAreaItemViewModel
{
    public Guid FamilyGroupId { get; set; }

    public string FamilyGroupName { get; set; } = string.Empty;

    public Guid AreaId { get; set; }

    public string AreaName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwę pozycji.")]
    [MaxLength(160)]
    [Display(Name = "Nazwa kosztu")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz kategorię.")]
    [Display(Name = "Kategoria")]
    public string CategoryCode { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true,
        ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Planowana kwota")]
    public decimal PlannedAmount { get; set; }

    [Required(ErrorMessage = "Wybierz częstotliwość.")]
    [Display(Name = "Częstotliwość")]
    public string FrequencyCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz sposób wyznaczania terminu.")]
    [Display(Name = "Termin w miesiącu")]
    public string DueDateModeCode { get; set; } = "SpecificDay";

    [Range(
        1,
        31,
        ErrorMessage = "Dzień terminu musi mieścić się w zakresie 1-31.")]
    [Display(Name = "Dzień miesiąca")]
    public int DueDay { get; set; } = 1;

    [Range(
        1,
        27,
        ErrorMessage = "Podaj od 1 do 27 dni przed końcem miesiąca.")]
    [Display(Name = "Ile dni przed końcem miesiąca")]
    public int? DaysBeforeEnd { get; set; }

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

    public List<SelectListItem> DueDateModes { get; set; } = [];

    public List<SelectListItem> Beneficiaries { get; set; } = [];
}
