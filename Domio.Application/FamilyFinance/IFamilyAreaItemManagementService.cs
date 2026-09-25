namespace Domio.Application.FamilyFinance;

public interface IFamilyAreaItemManagementService
{
    Task<EditFamilyAreaItemContext?> GetEditContextAsync(
        Guid ruleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        UpdateFamilyAreaItemRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task EndAsync(
        Guid ruleId,
        DateTime endDateUtc,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
