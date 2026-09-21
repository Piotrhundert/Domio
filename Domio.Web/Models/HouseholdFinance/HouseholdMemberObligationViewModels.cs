using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.HouseholdFinance;

public sealed class CreateHouseholdMemberObligationViewModel
{
    [Required(ErrorMessage = "Wybierz domownika.")]
    [Display(Name = "Domownik")]
    public Guid HouseholdMemberId { get; set; }

    [Required(ErrorMessage = "Wybierz źródło zobowiązania.")]
    [Display(Name = "Źródło")]
    public string SourceTypeCode { get; set; } = string.Empty;

    [Display(Name = "Faktura domu")]
    public Guid? SourceInvoiceId { get; set; }

    [Required(ErrorMessage = "Podaj opis zobowiązania.")]
    [MaxLength(500)]
    [Display(Name = "Opis / powód")]
    public string Description { get; set; } = string.Empty;

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Kwota zobowiązania")]
    public decimal Amount { get; set; } = 0.01m;

    [DataType(DataType.Date)]
    [Display(Name = "Termin wpłaty")]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);

    [Required(ErrorMessage = "Wybierz konto domu.")]
    [Display(Name = "Konto docelowe domu")]
    public Guid TargetHouseholdAccountId { get; set; }

    public List<SelectListItem> Members { get; set; } = [];

    public List<SelectListItem> SourceTypes { get; set; } = [];

    public List<SelectListItem> Invoices { get; set; } = [];

    public List<SelectListItem> TargetAccounts { get; set; } = [];
}

public sealed class HouseholdMemberObligationPaymentViewModel
{
    [Required]
    public Guid ObligationId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string SourceDisplayName { get; set; } = string.Empty;

    public decimal ObligationAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public DateTime DueDateUtc { get; set; }

    public string CurrencyCode { get; set; } = "PLN";

    public string TargetHouseholdAccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Wybierz konto prywatne.")]
    [Display(Name = "Konto źródłowe")]
    public Guid SourcePersonalAccountId { get; set; }

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage = "Kwota wpłaty musi być większa od zera.")]
    [Display(Name = "Kwota wpłaty")]
    public decimal Amount { get; set; }

    public List<SelectListItem> SourceAccounts { get; set; } = [];
}
