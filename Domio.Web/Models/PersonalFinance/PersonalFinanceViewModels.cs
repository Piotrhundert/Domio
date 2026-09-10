using System.ComponentModel.DataAnnotations;
using Domio.Application.PersonalFinance;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.PersonalFinance;

public sealed class CreatePersonalAccountViewModel
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
            "Kod waluty musi mieć 3 litery.")]
    [RegularExpression(
        "^[A-Za-z]{3}$",
        ErrorMessage =
            "Kod waluty może zawierać tylko litery.")]
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

    public List<SelectListItem> AccountTypes
    {
        get;
        set;
    } = [];
}

public sealed class AddPersonalOperationViewModel
{
    [Required]
    [Display(Name = "Konto")]
    public Guid AccountId { get; set; }

    [Required(ErrorMessage = "Wybierz rodzaj operacji.")]
    [Display(Name = "Rodzaj operacji")]
    public string KindCode { get; set; } = string.Empty;

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Kwota")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data operacji / pierwszego wystąpienia")]
    public DateTime OccurredOn { get; set; } = DateTime.Today;

    [MaxLength(500)]
    [Display(Name = "Opis")]
    public string? Description { get; set; }

    [Display(Name = "Operacja cykliczna")]
    public bool IsRecurring { get; set; }

    [MaxLength(160)]
    [Display(Name = "Nazwa reguły cyklicznej")]
    public string? RecurringName { get; set; }

    [Display(Name = "Częstotliwość")]
    public string FrequencyCode { get; set; } = "Monthly";

    [Display(Name = "Kategoria")]
    public string CategoryCode { get; set; } = "OtherExpense";

    [MaxLength(200)]
    [Display(Name = "Firma / instytucja / usługodawca")]
    public string? Counterparty { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data końca")]
    public DateTime? RecurringEndOn { get; set; }

    public List<SelectListItem> Accounts { get; set; } = [];

    public List<SelectListItem> OperationKinds { get; set; } = [];

    public List<SelectListItem> RecurringFrequencies { get; set; } = [];

    public List<SelectListItem> Categories { get; set; } = [];
}


public sealed class EditPersonalRecurringRuleViewModel
{
    [Required]
    public Guid RuleId { get; set; }

    [Required]
    [Display(Name = "Konto")]
    public Guid AccountId { get; set; }

    [Required(ErrorMessage = "Wybierz rodzaj operacji.")]
    [Display(Name = "Rodzaj")]
    public string KindCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwę reguły.")]
    [MaxLength(160)]
    [Display(Name = "Nazwa reguły")]
    public string Name { get; set; } = string.Empty;

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Planowana kwota")]
    public decimal PlannedAmount { get; set; }

    [Required]
    [Display(Name = "Częstotliwość")]
    public string FrequencyCode { get; set; } = "Monthly";

    [Required]
    [Display(Name = "Kategoria")]
    public string CategoryCode { get; set; } = "Subscription";

    [MaxLength(200)]
    [Display(Name = "Firma / instytucja / usługodawca")]
    public string? Counterparty { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data początku")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "Data końca")]
    public DateTime? EndDate { get; set; }

    public List<SelectListItem> Accounts { get; set; } = [];

    public List<SelectListItem> OperationKinds { get; set; } = [];

    public List<SelectListItem> RecurringFrequencies { get; set; } = [];

    public List<SelectListItem> Categories { get; set; } = [];
}

public sealed class ConfirmRecurringOccurrenceViewModel
{
    [Required]
    public Guid OccurrenceId { get; set; }

    public string RuleName { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string KindCode { get; set; } = string.Empty;

    public string KindNamePl { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    public decimal PlannedAmount { get; set; }

    public DateTime PlannedDate { get; set; }

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage = "Kwota musi być większa od zera.")]
    [Display(Name = "Rzeczywista kwota")]
    public decimal ActualAmount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Rzeczywista data")]
    public DateTime ActualDate { get; set; } = DateTime.Today;

    [MaxLength(500)]
    [Display(Name = "Opis")]
    public string? Description { get; set; }
}

public sealed class DeletePersonalRecurringRuleViewModel
{
    public Guid RuleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public string KindNamePl { get; set; } = string.Empty;

    public decimal PlannedAmount { get; set; }

    public string CurrencyCode { get; set; } = "PLN";

    public string FrequencyNamePl { get; set; } = string.Empty;
}


public sealed class PersonalTransferViewModel
{
    [Required]
    [Display(Name = "Z konta")]
    public Guid SourceAccountId { get; set; }

    [Required]
    [Display(Name = "Na konto")]
    public Guid TargetAccountId { get; set; }

    [Range(
        0.01d,
        999999999999.99d,
        ErrorMessage =
            "Kwota transferu musi być większa od zera.")]
    [Display(Name = "Kwota")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data transferu")]
    public DateTime OccurredOn { get; set; } =
        DateTime.Today;

    [MaxLength(300)]
    [Display(Name = "Opis")]
    public string? Description { get; set; }

    public List<SelectListItem> SourceAccounts { get; set; } = [];

    public List<SelectListItem> TargetAccounts { get; set; } = [];
}


public sealed class ClosePersonalAccountViewModel
{
    public Guid AccountId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountTypeNamePl { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    public decimal Balance { get; set; }

    public bool IsActive { get; set; }

    public int ActiveRecurringRules { get; set; }

    public int PlannedRecurringOccurrences { get; set; }

    public bool CanClose =>
        IsActive &&
        Balance == 0m &&
        ActiveRecurringRules == 0 &&
        PlannedRecurringOccurrences == 0;
}


public sealed class CorrectPersonalTransactionViewModel
{
    [Required]
    public Guid TransactionId { get; set; }

    public string AccountName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    public string KindNamePl { get; set; } = string.Empty;

    public decimal OriginalAmount { get; set; }

    public DateTime OccurredAt { get; set; }

    public string? OriginalDescription { get; set; }

    [Range(
        0d,
        999999999999.99d,
        ErrorMessage =
            "Prawidłowa kwota nie może być ujemna.")]
    [Display(Name = "Prawidłowa kwota")]
    public decimal CorrectedAmount { get; set; }

    [Display(Name = "Kategoria")]
    public string? CategoryCode { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];
}


public sealed class PersonalTransactionHistoryViewModel
{
    [DataType(DataType.Date)]
    [Display(Name = "Od daty")]
    public DateTime? FromDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Do daty")]
    public DateTime? ToDate { get; set; }

    [Display(Name = "Konto")]
    public Guid? AccountId { get; set; }

    [Display(Name = "Rodzaj operacji")]
    public string? KindCode { get; set; }

    [Display(Name = "Kategoria")]
    public string? CategoryCode { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    public int TotalCount { get; set; }

    public int TotalPages { get; set; } = 1;

    public List<PersonalTransactionHistoryItem> Items { get; set; } = [];

    public List<SelectListItem> Accounts { get; set; } = [];

    public List<SelectListItem> OperationKinds { get; set; } = [];

    public List<SelectListItem> Categories { get; set; } = [];
}
