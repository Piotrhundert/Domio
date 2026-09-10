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

public sealed record PersonalFinanceOverview(
    Guid OwnerPersonId,
    IReadOnlyList<PersonalAccountSummary> Accounts,
    IReadOnlyList<PersonalTransactionItem> RecentTransactions);

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
