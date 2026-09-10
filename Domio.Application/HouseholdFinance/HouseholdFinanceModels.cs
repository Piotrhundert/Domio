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
