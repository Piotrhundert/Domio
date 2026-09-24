namespace Domio.Application.FinancialGoals;

public interface IFinancialGoalService
{
    Task<FinancialGoalOverview> GetOverviewAsync(
        string scopeCode,
        Guid? familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<FinancialGoalDetails?> GetDetailsAsync(
        Guid goalId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(
        CreateFinancialGoalRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        UpdateFinancialGoalRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddContributionAsync(
        AddFinancialGoalContributionRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        Guid goalId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
