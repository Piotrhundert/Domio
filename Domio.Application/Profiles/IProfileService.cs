namespace Domio.Application.Profiles;

public interface IProfileService
{
    Task<PersonProfileData?> GetAsync(
        Guid personId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        UpdatePersonProfileRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
