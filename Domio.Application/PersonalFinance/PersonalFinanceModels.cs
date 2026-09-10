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
    DateTime CreatedAtUtc);

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
    string? Description);

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
