
namespace Domio.Application.HouseholdFinance;

public sealed record HouseholdInvoiceSummary(
    Guid InvoiceId,
    string Supplier,
    string InvoiceNumber,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    decimal GrossAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string StatusCode,
    string StatusNamePl,
    string CategoryCode,
    string CategoryNamePl,
    Guid? UtilityInvoiceId,
    DateTime CreatedAtUtc);

public sealed record HouseholdInvoicePaymentItem(
    Guid PaymentId,
    Guid InvoiceId,
    string Supplier,
    string InvoiceNumber,
    Guid HouseholdAccountId,
    string HouseholdAccountName,
    decimal Amount,
    string CurrencyCode,
    DateTime PaidAtUtc,
    Guid HouseholdEntryId);

public sealed record HouseholdInvoiceOverview(
    Guid HouseholdId,
    string HouseholdName,
    string CurrencyCode,
    bool CanManage,
    IReadOnlyList<HouseholdAccountSummary> Accounts,
    IReadOnlyList<HouseholdInvoiceSummary> Invoices,
    IReadOnlyList<HouseholdInvoicePaymentItem> RecentPayments);

public sealed record CreateHouseholdInvoiceRequest(
    string Supplier,
    string InvoiceNumber,
    DateTime IssueDateUtc,
    DateTime DueDateUtc,
    decimal GrossAmount,
    string CategoryCode,
    Guid? UtilityInvoiceId = null,
    DateTime? BillingPeriodFromUtc = null,
    DateTime? BillingPeriodToUtc = null,
    string? MainMeterNumber = null,
    string? MainMeterUnit = null,
    string? MainMeterPreviousReading = null,
    string? MainMeterCurrentReading = null,
    string? SubmeterReadingsSnapshot = null);

public sealed record HouseholdInvoicePaymentAccount(
    Guid AccountId,
    string Name,
    string AccountTypeNamePl,
    string CurrencyCode,
    decimal Balance);

public sealed record HouseholdInvoicePaymentForm(
    Guid InvoiceId,
    string Supplier,
    string InvoiceNumber,
    DateTime DueDateUtc,
    decimal GrossAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    string CategoryNamePl,
    string CurrencyCode,
    IReadOnlyList<HouseholdInvoicePaymentAccount> Accounts);

public sealed record PayHouseholdInvoiceRequest(
    Guid CommandId,
    Guid InvoiceId,
    Guid HouseholdAccountId,
    decimal Amount,
    DateTime PaidAtUtc);
