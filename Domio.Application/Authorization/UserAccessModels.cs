namespace Domio.Application.Authorization;

public sealed record UserPermissionGrant(
    string Code,
    string ScopeCode);

public sealed record UserAccessSnapshot(
    Guid UserId,
    Guid PersonId,
    string LoginName,
    string DisplayName,
    string RoleCode,
    string RoleNamePl,
    IReadOnlyList<UserPermissionGrant> Permissions);
