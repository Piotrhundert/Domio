namespace Domio.Application.Authentication;

public interface IAccountAuthenticationService
{
    Task<bool> HasAnyUserAsync(
        CancellationToken cancellationToken = default);

    Task<FirstAdministratorSetupResult> InitializeFirstAdministratorAsync(
        FirstAdministratorSetupRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> AuthenticateAsync(
        string loginName,
        string password,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task RecordLogoutAsync(
        Guid userId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
