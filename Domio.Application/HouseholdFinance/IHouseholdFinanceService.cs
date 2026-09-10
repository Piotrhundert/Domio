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
}
