namespace Domio.Application.HouseholdFinance;

public sealed record HouseholdMemberObligationMemberOption(
    Guid HouseholdMemberId,
    string DisplayName);

public sealed record HouseholdMemberObligationInvoiceOption(
    Guid InvoiceId,
    string DisplayName,
    string CategoryNamePl,
    decimal GrossAmount,
    decimal RemainingAmount);

public sealed record HouseholdMemberObligationSummary(
    Guid ObligationId,
    Guid HouseholdMemberId,
    string HouseholdMemberName,
    string SourceTypeCode,
    string SourceTypeNamePl,
    Guid? SourceInvoiceId,
    string? SourceInvoiceDisplayName,
    string Description,
    decimal Amount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DateTime DueDateUtc,
    string StatusCode,
    string StatusNamePl,
    Guid TargetHouseholdAccountId,
    string TargetHouseholdAccountName,
    string CurrencyCode,
    bool IsOwn);

public sealed record HouseholdMemberObligationPaymentRequestItem(
    Guid PaymentRequestId,
    Guid ObligationId,
    Guid HouseholdMemberId,
    string HouseholdMemberName,
    string Description,
    decimal Amount,
    string CurrencyCode,
    string StatusCode,
    string StatusNamePl,
    DateTime SubmittedAtUtc,
    DateTime? ReviewedAtUtc,
    string? ReviewNote,
    bool IsOwn,
    string? SourcePersonalAccountName);

public sealed record HouseholdMemberObligationOverview(
    Guid HouseholdId,
    string HouseholdName,
    string CurrencyCode,
    bool CanManage,
    bool CanApprove,
    IReadOnlyList<HouseholdMemberObligationMemberOption> Members,
    IReadOnlyList<HouseholdAccountSummary> TargetAccounts,
    IReadOnlyList<HouseholdMemberObligationInvoiceOption> Invoices,
    IReadOnlyList<HouseholdMemberObligationSummary> Obligations,
    IReadOnlyList<HouseholdMemberObligationPaymentRequestItem> PaymentRequests);

public sealed record CreateHouseholdMemberObligationRequest(
    Guid HouseholdMemberId,
    string SourceTypeCode,
    Guid? SourceInvoiceId,
    string Description,
    decimal Amount,
    DateTime DueDateUtc,
    Guid TargetHouseholdAccountId);

public sealed record HouseholdMemberObligationPaymentForm(
    Guid ObligationId,
    string Description,
    string SourceDisplayName,
    decimal ObligationAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DateTime DueDateUtc,
    string CurrencyCode,
    string TargetHouseholdAccountName,
    IReadOnlyList<HouseholdContributionPaymentSourceAccount> SourceAccounts);

public sealed record SubmitHouseholdMemberObligationPaymentRequest(
    Guid ObligationId,
    Guid SourcePersonalAccountId,
    decimal Amount);

public sealed record ReviewHouseholdMemberObligationPaymentRequest(
    Guid PaymentRequestId,
    string? ReviewNote);
