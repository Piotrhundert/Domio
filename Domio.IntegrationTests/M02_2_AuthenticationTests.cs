using Domio.Application.Authentication;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M02_2_AuthenticationTests
{
    [Fact]
    public async Task First_admin_login_and_lockout_should_work()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-2-{Guid.NewGuid():N}");

        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "domio-test.db");

        try
        {
            var options =
                new DbContextOptionsBuilder<DomioDbContext>()
                    .UseSqlite(
                        $"Data Source={databasePath};Foreign Keys=True;Pooling=False")
                    .Options;

            await using var dbContext = new DomioDbContext(options);
            await dbContext.Database.MigrateAsync();

            var auditService = new AuditService(dbContext);
            var authenticationService =
                new AccountAuthenticationService(dbContext, auditService);

            Assert.False(await authenticationService.HasAnyUserAsync());

            var setup =
                await authenticationService.InitializeFirstAdministratorAsync(
                    new FirstAdministratorSetupRequest(
                        "Jan",
                        "Testowy",
                        "admin",
                        "DomioTest123"),
                    Guid.NewGuid().ToString("N"));

            Assert.Equal("admin", setup.LoginName);
            Assert.True(await authenticationService.HasAnyUserAsync());

            var stored = await dbContext.UserAccounts
                .AsNoTracking()
                .SingleAsync();

            Assert.NotNull(stored.PasswordHash);
            Assert.DoesNotContain("DomioTest123", stored.PasswordHash!);

            var success =
                await authenticationService.AuthenticateAsync(
                    "ADMIN",
                    "DomioTest123",
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(AuthenticationStatus.Success, success.Status);
            Assert.NotNull(success.User);
            Assert.Equal("Administrator", success.User!.RoleCode);
            Assert.Equal("Administrator", success.User.RoleNamePl);

            for (var i = 0; i < 4; i++)
            {
                var failed =
                    await authenticationService.AuthenticateAsync(
                        "admin",
                        "ZleHaslo123",
                        Guid.NewGuid().ToString("N"));

                Assert.Equal(
                    AuthenticationStatus.InvalidCredentials,
                    failed.Status);
            }

            var locked =
                await authenticationService.AuthenticateAsync(
                    "admin",
                    "ZleHaslo123",
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(AuthenticationStatus.Locked, locked.Status);
            Assert.NotNull(locked.LockoutEndUtc);

            var stillLocked =
                await authenticationService.AuthenticateAsync(
                    "admin",
                    "DomioTest123",
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(AuthenticationStatus.Locked, stillLocked.Status);

            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => authenticationService
                        .InitializeFirstAdministratorAsync(
                            new FirstAdministratorSetupRequest(
                                "Drugi",
                                "Administrator",
                                "admin2",
                                "DomioTest456"),
                            Guid.NewGuid().ToString("N")));

            Assert.Contains("już utworzone", exception.Message);

            var schemaVersion = await dbContext.SchemaVersions
                .AsNoTracking()
                .Where(x => x.Id == 1)
                .Select(x => x.Version)
                .SingleAsync();

            Assert.True(schemaVersion >= 4);
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
