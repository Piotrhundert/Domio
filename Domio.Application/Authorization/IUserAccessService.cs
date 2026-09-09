namespace Domio.Application.Authorization;

public interface IUserAccessService
{
    Task<UserAccessSnapshot?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
