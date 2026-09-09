namespace Domio.Application.Users;

public sealed record PermissionDirectoryItem(
    string Code,
    string ModuleCode,
    string ModuleNamePl,
    string NamePl,
    string DescriptionPl,
    string RiskLevel,
    IReadOnlyList<string> AllowedScopeCodes);

public sealed record RolePermissionDirectoryItem(
    string Code,
    string ModuleCode,
    string ModuleNamePl,
    string NamePl,
    string DescriptionPl,
    string RiskLevel,
    string ScopeCode,
    string ScopeNamePl);

public sealed record RoleWithPermissionsItem(
    int Id,
    string Code,
    string NamePl,
    string DescriptionPl,
    bool IsSystem,
    bool UsesDefaultPermissions,
    int AssignedUsers,
    IReadOnlyList<RolePermissionDirectoryItem> Permissions);

public sealed record RolePermissionsOverview(
    IReadOnlyList<RoleWithPermissionsItem> Roles,
    IReadOnlyList<PermissionDirectoryItem> PermissionCatalog);

public sealed record RoleEditPermissionData(
    string Code,
    string ModuleCode,
    string ModuleNamePl,
    string NamePl,
    string DescriptionPl,
    string RiskLevel,
    bool IsAssigned,
    string ScopeCode,
    IReadOnlyList<string> AllowedScopeCodes,
    bool IsProtected);

public sealed record RoleEditData(
    int RoleId,
    string Code,
    string NamePl,
    string DescriptionPl,
    bool IsSystem,
    bool UsesDefaultPermissions,
    IReadOnlyList<RoleEditPermissionData> Permissions);

public sealed record UpdateRoleRequest(
    int RoleId,
    string NamePl,
    string DescriptionPl,
    IReadOnlyList<RolePermissionSelection> Permissions);

public sealed record RolePermissionSelection(
    string PermissionCode,
    string ScopeCode);
