namespace Domio.Application.Authentication;

public sealed record FirstAdministratorSetupRequest(
    string FirstName,
    string LastName,
    string LoginName,
    string Password);

public sealed record FirstAdministratorSetupResult(
    Guid UserId,
    Guid PersonId,
    string LoginName);
