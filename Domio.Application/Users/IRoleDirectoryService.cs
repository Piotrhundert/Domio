namespace Domio.Application.Users;

public interface IRoleDirectoryService
{
    Task<RolePermissionsOverview> GetOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<RoleEditData?> GetEditDataAsync(
        int roleId,
        CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(
        UpdateRoleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task ResetRolePermissionsAsync(
        int roleId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
