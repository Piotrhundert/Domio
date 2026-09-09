namespace Domio.Application.Authentication;

public enum AuthenticationStatus
{
    Success = 1,
    InvalidCredentials = 2,
    Locked = 3,
    Inactive = 4
}

public sealed record AuthenticationResult(
    AuthenticationStatus Status,
    AuthenticatedUser? User = null,
    DateTime? LockoutEndUtc = null);
