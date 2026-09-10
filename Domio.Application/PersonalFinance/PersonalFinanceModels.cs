namespace Domio.Application.PersonalFinance;

public sealed record PersonalAccountSummary(
    Guid AccountId,
    string Name,
    string AccountTypeCode,
    string AccountTypeNamePl,
    string CurrencyCode,
    decimal Balance,
    bool IsActive);

public sealed record PersonalTransactionItem(
    Guid TransactionId,
    Guid AccountId,
    string KindCode,
    string KindNamePl,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Description,
    Guid? CorrectsTransactionId,
    DateTime CreatedAtUtc,
    string? CategoryCode = null,
    string? CategoryNamePl = null,
    string? Counterparty = null);

public sealed record PersonalRecurringRuleSummary(
    Guid RuleId,
    Guid AccountId,
    string AccountName,
    string KindCode,
    string KindNamePl,
    string Name,
    decimal PlannedAmount,
    string CurrencyCode,
    string FrequencyCode,
    string FrequencyNamePl,
    string CategoryCode,
    string CategoryNamePl,
    string? Counterparty,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    bool IsActive);

public sealed record PersonalRecurringOccurrenceItem(
    Guid OccurrenceId,
    Guid RuleId,
    Guid AccountId,
    string AccountName,
    string RuleName,
    string KindCode,
    string KindNamePl,
    string CategoryNamePl,
    string? Counterparty,
    string PeriodKey,
    DateTime PlannedDateUtc,
    decimal PlannedAmount,
    string CurrencyCode,
    string StatusCode,
    string StatusNamePl,
    Guid? ActualTransactionId,
    decimal? ActualAmount,
    DateTime? ActualDateUtc);

public sealed record PersonalFinanceOverview(
    Guid OwnerPersonId,
    IReadOnlyList<PersonalAccountSummary> Accounts,
    IReadOnlyList<PersonalTransactionItem> RecentTransactions,
    IReadOnlyList<PersonalRecurringRuleSummary> RecurringRules,
    IReadOnlyList<PersonalRecurringOccurrenceItem> RecurringOccurrences);

public sealed record CreatePersonalAccountRequest(
    string Name,
    string AccountTypeCode,
    string CurrencyCode,
    decimal InitialBalance);

public sealed record PostPersonalOperationRequest(
    Guid AccountId,
    string KindCode,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Description,
    string? CategoryCode = null,
    string? Counterparty = null);

public sealed record CreatePersonalRecurringRuleRequest(
    Guid AccountId,
    string KindCode,
    string Name,
    decimal PlannedAmount,
    string FrequencyCode,
    string CategoryCode,
    string? Counterparty,
    DateTime StartDateUtc,
    DateTime? EndDateUtc);

public sealed record UpdatePersonalRecurringRuleRequest(
    Guid RuleId,
    Guid AccountId,
    string KindCode,
    string Name,
    decimal PlannedAmount,
    string FrequencyCode,
    string CategoryCode,
    string? Counterparty,
    DateTime StartDateUtc,
    DateTime? EndDateUtc);

public sealed record ConfirmPersonalRecurringOccurrenceRequest(
    Guid OccurrenceId,
    decimal ActualAmount,
    DateTime ActualDateUtc,
    string? Description);


public sealed record CreatePersonalTransferRequest(
    Guid SourceAccountId,
    Guid TargetAccountId,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Description);

public sealed record PersonalTransferResult(
    Guid SourceTransactionId,
    Guid TargetTransactionId,
    Guid SourceAccountId,
    Guid TargetAccountId,
    decimal Amount,
    string CurrencyCode);


public sealed record PersonalAccountClosureInfo(
    Guid AccountId,
    string Name,
    string AccountTypeNamePl,
    string CurrencyCode,
    decimal Balance,
    bool IsActive,
    int ActiveRecurringRules,
    int PlannedRecurringOccurrences)
{
    public bool CanClose =>
        IsActive &&
        Balance == 0m &&
        ActiveRecurringRules == 0 &&
        PlannedRecurringOccurrences == 0;
}


public sealed record PersonalTransactionCorrectionInfo(
    Guid TransactionId,
    Guid AccountId,
    string AccountName,
    string CurrencyCode,
    string KindCode,
    string KindNamePl,
    decimal OriginalAmount,
    DateTime OccurredAtUtc,
    string? CategoryCode,
    string CategoryNamePl,
    string? Counterparty,
    string? Description,
    bool AlreadyCorrected,
    bool CanCorrect);

public sealed record CorrectPersonalTransactionRequest(
    Guid TransactionId,
    decimal CorrectedAmount,
    string? CategoryCode);


public sealed record PersonalTransactionHistoryFilter(
    DateTime? FromDateUtc,
    DateTime? ToDateUtc,
    Guid? AccountId,
    string? KindCode,
    string? CategoryCode,
    int Page = 1,
    int PageSize = 50);

public sealed record PersonalTransactionHistoryItem(
    Guid TransactionId,
    Guid AccountId,
    string AccountName,
    string CurrencyCode,
    string KindCode,
    string KindNamePl,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? CategoryCode,
    string CategoryNamePl,
    string? Counterparty,
    string? Description,
    Guid? CorrectsTransactionId,
    bool HasCorrection);

public sealed record PersonalTransactionHistoryResult(
    IReadOnlyList<PersonalTransactionHistoryItem> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages =>
        TotalCount == 0
            ? 1
            : (int)Math.Ceiling(
                TotalCount /
                (double)PageSize);
}
