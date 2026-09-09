using Domio.Application.Users;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Users;

public sealed class UserDirectoryService(
    DomioDbContext dbContext) : IUserDirectoryService
{
    public async Task<UserDirectoryOverview> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var users = await (
            from account in dbContext.UserAccounts.AsNoTracking()
            join person in dbContext.People.AsNoTracking()
                on account.PersonId equals person.Id
            join role in dbContext.RoleDefinitions.AsNoTracking()
                on account.RoleDefinitionId equals role.Id
            orderby person.LastName, person.FirstName, account.LoginName
            select new UserDirectoryItem(
                account.Id,
                person.Id,
                string.IsNullOrWhiteSpace(person.DisplayName)
                    ? (person.FirstName + " " + person.LastName).Trim()
                    : person.DisplayName!,
                account.LoginName,
                role.Code,
                role.NamePl,
                account.IsActive && person.IsActive,
                account.LockoutEndUtc != null &&
                    account.LockoutEndUtc > now,
                account.LockoutEndUtc,
                account.LastLoginAtUtc,
                account.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var assignments = await dbContext.UserAccounts
            .AsNoTracking()
            .GroupBy(x => x.RoleDefinitionId)
            .Select(group => new
            {
                RoleId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(
                x => x.RoleId,
                x => x.Count,
                cancellationToken);

        var roles = await dbContext.RoleDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new RoleDirectoryItem(
                x.Id,
                x.Code,
                x.NamePl,
                x.DescriptionPl,
                x.IsSystem,
                assignments.ContainsKey(x.Id)
                    ? assignments[x.Id]
                    : 0))
            .ToListAsync(cancellationToken);

        return new UserDirectoryOverview(
            users,
            roles,
            users.Count,
            users.Count(x => x.IsActive),
            users.Count(x => x.IsLocked));
    }
}
