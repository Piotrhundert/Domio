using Domio.Domain.Users;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M02_1_UserRoleModelTests
{
    [Fact]
    public async Task Migration_should_create_people_users_and_five_system_roles()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-1-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);

        var databasePath =
            Path.Combine(root, "domio-test.db");

        try
        {
            var options =
                new DbContextOptionsBuilder<DomioDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                    .Options;

            await using var dbContext =
                new DomioDbContext(options);

            await dbContext.Database.MigrateAsync();

            var schemaVersion = await dbContext.SchemaVersions
                .AsNoTracking()
                .Where(x => x.Id == 1)
                .Select(x => x.Version)
                .SingleAsync();

            Assert.Equal(3, schemaVersion);

            var roles = await dbContext.RoleDefinitions
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToListAsync();

            Assert.Equal(5, roles.Count);
            Assert.Equal(
                new[]
                {
                    "Administrator",
                    "Domownik",
                    "Lokator",
                    "Gość",
                    "Dziecko"
                },
                roles.Select(x => x.NamePl).ToArray());

            var person = new Person
            {
                Id = Guid.NewGuid(),
                FirstName = "Test",
                LastName = "Administrator",
                Email = "admin@example.test",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var user = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = person.Id,
                RoleDefinitionId = SystemRoles.AdministratorId,
                LoginName = "admin",
                NormalizedLoginName = "ADMIN",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            dbContext.People.Add(person);
            dbContext.UserAccounts.Add(user);

            await dbContext.SaveChangesAsync();

            Assert.Equal(
                1,
                await dbContext.UserAccounts.CountAsync());

            Assert.Equal(
                SystemRoles.AdministratorId,
                await dbContext.UserAccounts
                    .Where(x => x.Id == user.Id)
                    .Select(x => x.RoleDefinitionId)
                    .SingleAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
