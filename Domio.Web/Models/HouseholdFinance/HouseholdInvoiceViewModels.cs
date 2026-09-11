
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.HouseholdFinance;

public sealed class CreateHouseholdInvoiceViewModel
{
    [Required(ErrorMessage = "Podaj dostawcę / kontrahenta.")]
    [MaxLength(200)]
    [Display(Name = "Dostawca / kontrahent")]
    public string Supplier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj numer faktury.")]
    [MaxLength(100)]
    [Display(Name = "Numer faktury")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Data wystawienia")]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "Termin płatności")]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage = "Kwota brutto musi być większa od zera.")]
    [Display(Name = "Kwota brutto")]
    public decimal GrossAmount { get; set; } = 0.01m;

    [Required(ErrorMessage = "Wybierz kategorię faktury.")]
    [Display(Name = "Kategoria")]
    public string CategoryCode { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Okres rozliczeniowy od")]
    public DateTime? BillingPeriodFrom { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Okres rozliczeniowy do")]
    public DateTime? BillingPeriodTo { get; set; }

    [MaxLength(100)]
    [Display(Name = "Numer / identyfikator licznika głównego")]
    public string? MainMeterNumber { get; set; }

    [MaxLength(20)]
    [Display(Name = "Jednostka")]
    public string? MainMeterUnit { get; set; }

    [MaxLength(40)]
    [Display(Name = "Stan poprzedni licznika głównego")]
    public string? MainMeterPreviousReading { get; set; }

    [MaxLength(40)]
    [Display(Name = "Stan bieżący licznika głównego")]
    public string? MainMeterCurrentReading { get; set; }

    [MaxLength(4000)]
    [Display(Name = "Stany podliczników / odczyty pomocnicze")]
    public string? SubmeterReadingsSnapshot { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];
}

public sealed class PayHouseholdInvoiceViewModel
{
    [Required]
    public Guid CommandId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid InvoiceId { get; set; }

    public string Supplier { get; set; } = string.Empty;

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public string CategoryNamePl { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    [Required(ErrorMessage = "Wybierz konto domu.")]
    [Display(Name = "Konto domu")]
    public Guid HouseholdAccountId { get; set; }

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage = "Kwota płatności musi być większa od zera.")]
    [Display(Name = "Kwota płatności")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data płatności")]
    public DateTime PaidOn { get; set; } = DateTime.Today;

    public List<SelectListItem> Accounts { get; set; } = [];
}
