namespace Domio.Application.Users;

public sealed record UserDirectoryItem(
    Guid UserId,
    Guid PersonId,
    string DisplayName,
    string LoginName,
    string RoleCode,
    string RoleNamePl,
    bool IsActive,
    bool IsLocked,
    DateTime? LockoutEndUtc,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAtUtc);

public sealed record RoleDirectoryItem(
    int Id,
    string Code,
    string NamePl,
    string DescriptionPl,
    bool IsSystem,
    int AssignedUsers);

public sealed record UserDirectoryOverview(
    IReadOnlyList<UserDirectoryItem> Users,
    IReadOnlyList<RoleDirectoryItem> Roles,
    int TotalUsers,
    int ActiveUsers,
    int LockedUsers);
