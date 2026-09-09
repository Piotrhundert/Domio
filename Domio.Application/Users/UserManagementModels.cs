namespace Domio.Application.Users;

public sealed record UserRoleOption(
    int Id,
    string Code,
    string NamePl,
    string DescriptionPl);

public sealed record AvailablePersonOption(
    Guid PersonId,
    string DisplayName,
    string? Email,
    string? Phone);

public sealed record UserManagementCreateOptions(
    IReadOnlyList<UserRoleOption> Roles,
    IReadOnlyList<AvailablePersonOption> PeopleWithoutAccount);

public sealed record CreatePersonRequest(
    string FirstName,
    string LastName,
    string? DisplayName,
    string? Email,
    string? Phone);

public sealed record CreateUserRequest(
    Guid? ExistingPersonId,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Phone,
    string Email,
    string Password,
    int RoleDefinitionId);

public sealed record UpdateUserRequest(
    Guid UserId,
    string FirstName,
    string LastName,
    string? DisplayName,
    string? Phone,
    string Email,
    bool IsActive);

public sealed record UserEditData(
    Guid UserId,
    Guid PersonId,
    string FirstName,
    string LastName,
    string? DisplayName,
    string? Phone,
    string LoginName,
    string? Email,
    bool IsActive,
    int RoleDefinitionId,
    string RoleNamePl);
