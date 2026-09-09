namespace Domio.Application.Authentication;

public sealed record AuthenticatedUser(
    Guid UserId,
    Guid PersonId,
    string LoginName,
    string DisplayName,
    string RoleCode,
    string RoleNamePl);
