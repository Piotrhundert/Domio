namespace Domio.Application.Users;

public interface IUserDirectoryService
{
    Task<UserDirectoryOverview> GetOverviewAsync(
        CancellationToken cancellationToken = default);
}
