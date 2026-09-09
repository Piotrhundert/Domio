using Domio.Application.Authentication;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M02_3_UserDirectoryTests
{
    [Fact]
    public async Task Directory_should_return_users_roles_and_statuses()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-3-{Guid.NewGuid():N}");

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

            var auditService =
                new AuditService(dbContext);

            var authenticationService =
                new AccountAuthenticationService(
                    dbContext,
                    auditService);

            await authenticationService
                .InitializeFirstAdministratorAsync(
                    new FirstAdministratorSetupRequest(
                        "Jan",
                        "Administrator",
                        "admin",
                        "DomioTest123"),
                    Guid.NewGuid().ToString("N"));

            var now = DateTime.UtcNow;

            var secondPerson = new Person
            {
                Id = Guid.NewGuid(),
                FirstName = "Anna",
                LastName = "Domownik",
                DisplayName = "Anna Domownik",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            var secondAccount = new UserAccount
            {
                Id = Guid.NewGuid(),
                PersonId = secondPerson.Id,
                RoleDefinitionId =
                    SystemRoles.HouseholdMemberId,
                LoginName = "anna",
                NormalizedLoginName = "ANNA",
                PasswordHash = "test-only",
                IsActive = true,
                FailedLoginAttempts = 5,
                LockoutEndUtc = now.AddMinutes(10),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.People.Add(secondPerson);
            dbContext.UserAccounts.Add(secondAccount);
            await dbContext.SaveChangesAsync();

            var directoryService =
                new UserDirectoryService(dbContext);

            var overview =
                await directoryService.GetOverviewAsync();

            Assert.Equal(2, overview.TotalUsers);
            Assert.Equal(2, overview.ActiveUsers);
            Assert.Equal(1, overview.LockedUsers);
            Assert.Equal(5, overview.Roles.Count);

            var administrator =
                Assert.Single(
                    overview.Users,
                    x => x.LoginName == "admin");

            Assert.Equal(
                "Administrator",
                administrator.RoleNamePl);

            var householdMember =
                Assert.Single(
                    overview.Users,
                    x => x.LoginName == "anna");

            Assert.Equal(
                "Domownik",
                householdMember.RoleNamePl);
            Assert.True(householdMember.IsLocked);

            var householdRole =
                Assert.Single(
                    overview.Roles,
                    x => x.Code ==
                        SystemRoles.HouseholdMemberCode);

            Assert.Equal(1, householdRole.AssignedUsers);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection
                .ClearAllPools();

            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
