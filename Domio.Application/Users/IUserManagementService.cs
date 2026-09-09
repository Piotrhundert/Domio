namespace Domio.Application.Users;

public interface IUserManagementService
{
    Task<UserManagementCreateOptions> GetCreateOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<Guid> CreatePersonAsync(
        CreatePersonRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateUserAsync(
        CreateUserRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<UserEditData?> GetEditDataAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdateUserAsync(
        UpdateUserRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
