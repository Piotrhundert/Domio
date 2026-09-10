using System.ComponentModel.DataAnnotations;
using Domio.Application.HouseholdFinance;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.HouseholdFinance;

public sealed class HouseholdFinanceIndexViewModel
{
    public HouseholdFinanceOverview? Overview { get; set; }
}

public sealed class CreateHouseholdAccountViewModel
{
    [Required(
        ErrorMessage =
            "Podaj nazwę konta.")]
    [MaxLength(120)]
    [Display(Name = "Nazwa konta")]
    public string Name { get; set; } =
        string.Empty;

    [Required(
        ErrorMessage =
            "Wybierz typ konta.")]
    [Display(Name = "Typ konta")]
    public string AccountTypeCode { get; set; } =
        string.Empty;

    [Required(
        ErrorMessage =
            "Podaj walutę.")]
    [StringLength(
        3,
        MinimumLength = 3,
        ErrorMessage =
            "Waluta musi mieć trzyliterowy kod, np. PLN.")]
    [Display(Name = "Waluta")]
    public string CurrencyCode { get; set; } =
        "PLN";

    [Range(
        0d,
        999999999999.99d,
        ErrorMessage =
            "Saldo początkowe nie może być ujemne.")]
    [Display(Name = "Saldo początkowe")]
    public decimal InitialBalance { get; set; }

    public List<SelectListItem> AccountTypes { get; set; } = [];
}

public sealed class AddHouseholdOperationViewModel
{
    [Required]
    [Display(Name = "Konto domu")]
    public Guid AccountId { get; set; }

    [Required(
        ErrorMessage =
            "Wybierz rodzaj operacji.")]
    [Display(Name = "Rodzaj")]
    public string EntryTypeCode { get; set; } =
        string.Empty;

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage =
            "Kwota musi być większa od zera.")]
    [Display(Name = "Kwota")]
    public decimal Amount { get; set; } =
        0.01m;

    [DataType(DataType.Date)]
    [Display(Name = "Data operacji")]
    public DateTime OccurredOn { get; set; } =
        DateTime.Today;

    [Display(Name = "Kategoria")]
    public string? CategoryCode { get; set; }

    [MaxLength(500)]
    [Display(Name = "Opis")]
    public string? Description { get; set; }

    public List<SelectListItem> Accounts { get; set; } = [];

    public List<SelectListItem> EntryTypes { get; set; } = [];

    public List<SelectListItem> Categories { get; set; } = [];
}
