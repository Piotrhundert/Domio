namespace Domio.Application.HouseholdFinance;

public interface IHouseholdFinanceService
{
    Task<HouseholdFinanceOverview?> GetOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAccountAsync(
        CreateHouseholdAccountRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> PostOperationAsync(
        PostHouseholdOperationRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdTransferResult> TransferBetweenAccountsAsync(
        CreateHouseholdTransferRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdAccountClosureInfo?> GetAccountClosureInfoAsync(
        Guid accountId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task CloseAccountAsync(
        Guid accountId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
