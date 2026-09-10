namespace Domio.Application.HouseholdFinance;

public sealed record HouseholdAccountSummary(
    Guid AccountId,
    string Name,
    string AccountTypeCode,
    string AccountTypeNamePl,
    string CurrencyCode,
    decimal Balance,
    bool IsActive);

public sealed record HouseholdEntryItem(
    Guid EntryId,
    Guid AccountId,
    string AccountName,
    string CurrencyCode,
    string EntryTypeCode,
    string EntryTypeNamePl,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? CategoryCode,
    string CategoryNamePl,
    string? Description,
    string? SourceType,
    string? SourceId,
    Guid? CorrectsEntryId);

public sealed record HouseholdFinanceOverview(
    Guid HouseholdId,
    string HouseholdName,
    string CurrencyCode,
    IReadOnlyList<HouseholdAccountSummary> Accounts,
    IReadOnlyList<HouseholdEntryItem> RecentEntries);

public sealed record CreateHouseholdAccountRequest(
    string Name,
    string AccountTypeCode,
    string CurrencyCode,
    decimal InitialBalance);

public sealed record PostHouseholdOperationRequest(
    Guid AccountId,
    string EntryTypeCode,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? CategoryCode,
    string? Description,
    string? SourceType = "Manual",
    string? SourceId = null);


public sealed record CreateHouseholdTransferRequest(
    Guid SourceAccountId,
    Guid TargetAccountId,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Description);

public sealed record HouseholdTransferResult(
    Guid TransferId,
    Guid SourceEntryId,
    Guid TargetEntryId,
    Guid SourceAccountId,
    Guid TargetAccountId,
    decimal Amount,
    string CurrencyCode);

public sealed record HouseholdAccountClosureInfo(
    Guid AccountId,
    string Name,
    string AccountTypeNamePl,
    string CurrencyCode,
    decimal Balance,
    bool IsActive)
{
    public bool CanClose =>
        IsActive &&
        Balance == 0m;
}
