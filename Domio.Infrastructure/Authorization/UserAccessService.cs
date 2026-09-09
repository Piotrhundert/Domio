using Domio.Application.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Authorization;

public sealed class UserAccessService(
    DomioDbContext dbContext) : IUserAccessService
{
    public async Task<UserAccessSnapshot?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var row = await (
            from account in dbContext.UserAccounts.AsNoTracking()
            join person in dbContext.People.AsNoTracking()
                on account.PersonId equals person.Id
            join role in dbContext.RoleDefinitions.AsNoTracking()
                on account.RoleDefinitionId equals role.Id
            where account.Id == userId &&
                  account.IsActive &&
                  person.IsActive
            select new
            {
                account.Id,
                account.PersonId,
                account.LoginName,
                PersonDisplayName = person.DisplayName,
                person.FirstName,
                person.LastName,
                RoleCode = role.Code,
                RoleNamePl = role.NamePl,
                role.PermissionConfigurationJson
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var displayName =
            string.IsNullOrWhiteSpace(row.PersonDisplayName)
                ? $"{row.FirstName} {row.LastName}".Trim()
                : row.PersonDisplayName.Trim();

        var permissions =
            RolePermissionConfigurationCodec
                .Resolve(
                    row.RoleCode,
                    row.PermissionConfigurationJson)
                .Select(grant =>
                    new UserPermissionGrant(
                        grant.PermissionCode,
                        grant.ScopeCode))
                .ToArray();

        return new UserAccessSnapshot(
            row.Id,
            row.PersonId,
            row.LoginName,
            displayName,
            row.RoleCode,
            row.RoleNamePl,
            permissions);
    }
}
