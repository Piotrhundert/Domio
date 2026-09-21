namespace Domio.Application.FamilyFinance;

public interface IFamilyFinanceService
{
    Task<FamilyFinanceOverview> GetOverviewAsync(
        Guid actorUserId,
        Guid? familyGroupId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateGroupAsync(
        CreateFamilyGroupRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<AddFamilyMemberForm?> GetAddMemberFormAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddMemberAsync(
        AddFamilyMemberRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task EndMembershipAsync(
        Guid membershipId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<FamilySharingSnapshot?> GetOwnSharingAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task UpdateOwnSharingAsync(
        UpdateFamilySharingRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
