namespace Domio.Application.PersonalFinance;

public interface IPersonalFinanceService
{
    Task<PersonalFinanceOverview> GetOwnOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateOwnAccountAsync(
        CreatePersonalAccountRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> PostOwnOperationAsync(
        PostPersonalOperationRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
