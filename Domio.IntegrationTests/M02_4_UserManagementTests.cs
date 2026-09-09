using Domio.Application.Authentication;
using Domio.Application.Users;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M02_4_UserManagementTests
{
    [Fact]
    public async Task Automatic_login_create_edit_link_person_and_uniqueness_should_work()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-4-{Guid.NewGuid():N}");

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

            var auditService = new AuditService(dbContext);
            var authenticationService =
                new AccountAuthenticationService(
                    dbContext,
                    auditService);

            var admin =
                await authenticationService
                    .InitializeFirstAdministratorAsync(
                        new FirstAdministratorSetupRequest(
                            "Jan",
                            "Administrator",
                            "admin",
                            "DomioTest123"),
                        Guid.NewGuid().ToString("N"));

            var management =
                new UserManagementService(
                    dbContext,
                    auditService);

            var personOnlyId =
                await management.CreatePersonAsync(
                    new CreatePersonRequest(
                        "Anna",
                        "Domownik",
                        null,
                        "anna@example.test",
                        "500600700"),
                    admin.UserId,
                    Guid.NewGuid().ToString("N"));

            var linkedUserId =
                await management.CreateUserAsync(
                    new CreateUserRequest(
                        personOnlyId,
                        null,
                        null,
                        null,
                        null,
                        "anna@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    admin.UserId,
                    Guid.NewGuid().ToString("N"));

            var linked = await dbContext.UserAccounts
                .AsNoTracking()
                .SingleAsync(x => x.Id == linkedUserId);

            Assert.Equal(
                "Anna_Domownik",
                linked.LoginName);

            var authentication =
                await authenticationService.AuthenticateAsync(
                    "Anna_Domownik",
                    "DomioAnna123",
                    Guid.NewGuid().ToString("N"));

            Assert.Equal(
                AuthenticationStatus.Success,
                authentication.Status);

            var duplicateNameUserId =
                await management.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Anna",
                        "Domownik",
                        null,
                        null,
                        "anna2@example.test",
                        "DomioAnna456",
                        SystemRoles.GuestId),
                    admin.UserId,
                    Guid.NewGuid().ToString("N"));

            var duplicateNameAccount =
                await dbContext.UserAccounts
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == duplicateNameUserId);

            Assert.Equal(
                "Anna_Domownik_2",
                duplicateNameAccount.LoginName);

            var duplicateEmail =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => management.CreateUserAsync(
                        new CreateUserRequest(
                            null,
                            "Inna",
                            "Osoba",
                            null,
                            null,
                            "ANNA@EXAMPLE.TEST",
                            "DomioTest456",
                            SystemRoles.GuestId),
                        admin.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "e-mail",
                duplicateEmail.Message);

            await management.UpdateUserAsync(
                new UpdateUserRequest(
                    linkedUserId,
                    "Anna",
                    "Nowak",
                    "Ania Nowak",
                    "501501501",
                    "anna.nowak@example.test",
                    false),
                admin.UserId,
                Guid.NewGuid().ToString("N"));

            var edited =
                await management.GetEditDataAsync(
                    linkedUserId);

            Assert.NotNull(edited);
            Assert.Equal(
                "Nowak",
                edited!.LastName);
            Assert.Equal(
                "Anna_Domownik",
                edited.LoginName);
            Assert.False(edited.IsActive);

            var selfDisable =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => management.UpdateUserAsync(
                        new UpdateUserRequest(
                            admin.UserId,
                            "Jan",
                            "Administrator",
                            null,
                            null,
                            "admin@example.test",
                            false),
                        admin.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "własnego konta",
                selfDisable.Message);

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.Equal(5, schemaVersion);
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
