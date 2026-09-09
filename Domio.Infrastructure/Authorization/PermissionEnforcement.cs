using Domio.Domain.Users;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Authorization;

public static class PermissionEnforcement
{
    public static async Task<RolePermissionGrant>
        EnsureUserHasAsync(
            DomioDbContext dbContext,
            Guid userId,
            string permissionCode,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            throw new ArgumentException(
                "Kod uprawnienia nie może być pusty.",
                nameof(permissionCode));
        }

        var accessRow = await (
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
                role.Code,
                role.PermissionConfigurationJson
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (accessRow is null)
        {
            throw new UnauthorizedAccessException(
                "Konto wykonujące operację nie jest aktywne.");
        }

        var grant =
            RolePermissionConfigurationCodec
                .Resolve(
                    accessRow.Code,
                    accessRow.PermissionConfigurationJson)
                .SingleOrDefault(
                    x => string.Equals(
                        x.PermissionCode,
                        permissionCode,
                        StringComparison.Ordinal));

        if (grant is null)
        {
            throw new UnauthorizedAccessException(
                $"Brak wymaganego uprawnienia: {permissionCode}.");
        }

        return grant;
    }
}
