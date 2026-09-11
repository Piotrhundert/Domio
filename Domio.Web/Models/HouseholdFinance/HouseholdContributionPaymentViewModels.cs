using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.HouseholdFinance;

public sealed class HouseholdContributionPaymentViewModel
{
    public Guid ObligationId { get; set; }

    public string PeriodKey { get; set; } =
        string.Empty;

    public decimal ObligationAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public DateTime DueDateUtc { get; set; }

    public string CurrencyCode { get; set; } =
        "PLN";

    public string TargetHouseholdAccountName { get; set; } =
        string.Empty;

    [Required(
        ErrorMessage =
            "Wybierz prywatne konto, z którego ma zostać wysłana wpłata.")]
    [Display(Name = "Konto prywatne")]
    public Guid SourcePersonalAccountId { get; set; }

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage =
            "Kwota wpłaty musi być większa od zera.")]
    [Display(Name = "Kwota wpłaty")]
    public decimal Amount { get; set; }

    public List<SelectListItem> SourceAccounts { get; set; } = [];
}
