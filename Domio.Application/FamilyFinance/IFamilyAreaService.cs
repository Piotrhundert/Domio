namespace Domio.Application.FamilyFinance;

public interface IFamilyAreaService
{
    Task<FamilyAreaOverview> GetOverviewAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<FamilyAreaDetails?> GetDetailsAsync(
        Guid areaId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default);


    Task NormalizeScheduledOccurrencesAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAreaAsync(
        CreateFamilyAreaRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<CreateFamilyAreaItemContext?> GetCreateItemContextAsync(
        Guid areaId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateItemAsync(
        CreateFamilyAreaItemRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
