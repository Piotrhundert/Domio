using Domio.Application.Authentication;
using Domio.Application.Profiles;
using Domio.Application.Users;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.Profiles;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M02_7_ProfileTests
{
    [Fact]
    public async Task Profile_should_be_separate_from_account_and_sensitive_values_should_not_enter_audit()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-7-{Guid.NewGuid():N}");

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

            var userId =
                await management.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Anna",
                        "Nowak",
                        null,
                        null,
                        "anna.account@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    admin.UserId,
                    Guid.NewGuid().ToString("N"));

            var account =
                await dbContext.UserAccounts
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == userId);

            var profileService =
                new ProfileService(
                    dbContext,
                    auditService);

            await profileService.UpdateAsync(
                new UpdatePersonProfileRequest(
                    account.PersonId,
                    "Anna",
                    "Kowalska",
                    "Ania Kowalska",
                    PersonTypes.HouseholdMember,
                    "anna.contact@example.test",
                    "500600700",
                    "Notatka testowa",
                    new DateTime(1990, 4, 10),
                    "44051401458",
                    "polska",
                    IdentityDocumentTypes.IdentityCard,
                    "ABC123456",
                    "Polska",
                    new DateTime(2025, 1, 1),
                    new DateTime(2035, 1, 1),
                    PreferredContactMethods.Email,
                    "Polska",
                    "wielkopolskie",
                    "Poznań",
                    "60-001",
                    "Testowa",
                    "10",
                    "2",
                    null,
                    "Piotr",
                    "Kowalski",
                    "małżonek",
                    "501501501",
                    "piotr@example.test",
                    null),
                admin.UserId,
                Guid.NewGuid().ToString("N"));

            var profile =
                await profileService.GetAsync(
                    account.PersonId,
                    admin.UserId);

            Assert.NotNull(profile);
            Assert.Equal(
                "Kowalska",
                profile!.LastName);
            Assert.Equal(
                "anna.contact@example.test",
                profile.ContactEmail);
            Assert.Equal(
                "anna.account@example.test",
                profile.AccountEmail);
            Assert.Equal(
                "44051401458",
                profile.Pesel);
            Assert.NotNull(profile.Age);

            var audit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .Where(x =>
                        x.EventType ==
                        "M02.7.ProfileUpdated")
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .FirstAsync();

            Assert.DoesNotContain(
                "44051401458",
                audit.NewValuesJson ?? string.Empty);

            Assert.DoesNotContain(
                "ABC123456",
                audit.NewValuesJson ?? string.Empty);

            await management.UpdateUserAsync(
                new UpdateUserRequest(
                    userId,
                    "anna.login.changed@example.test",
                    false,
                    SystemRoles.HouseholdMemberId),
                admin.UserId,
                Guid.NewGuid().ToString("N"));

            var afterAccountChange =
                await dbContext.People
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == account.PersonId);

            Assert.True(
                afterAccountChange.IsActive);
            Assert.Equal(
                "anna.contact@example.test",
                afterAccountChange.Email);

            var invalidPesel =
                await Assert.ThrowsAsync<ArgumentException>(
                    () => profileService.UpdateAsync(
                        new UpdatePersonProfileRequest(
                            account.PersonId,
                            "Anna",
                            "Kowalska",
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            "12345678901",
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null),
                        admin.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "PESEL",
                invalidPesel.Message);

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.Equal(7, schemaVersion);
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
