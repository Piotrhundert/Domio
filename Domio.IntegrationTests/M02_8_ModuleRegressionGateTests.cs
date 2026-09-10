using Domio.Application.Authentication;
using Domio.Application.Authorization;
using Domio.Application.Profiles;
using Domio.Application.Users;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.Profiles;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M02_8_ModuleRegressionGateTests
{
    [Fact]
    public async Task G02_regression_should_preserve_identity_profiles_roles_and_permissions()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-8-{Guid.NewGuid():N}");

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

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.True(schemaVersion >= 7);
            Assert.Empty(
                await dbContext.Database
                    .GetPendingMigrationsAsync());

            var auditService =
                new AuditService(dbContext);

            var authenticationService =
                new AccountAuthenticationService(
                    dbContext,
                    auditService);

            var administrator =
                await authenticationService
                    .InitializeFirstAdministratorAsync(
                        new FirstAdministratorSetupRequest(
                            "Jan",
                            "Administrator",
                            "admin",
                            "DomioTest123"),
                        Guid.NewGuid().ToString("N"));

            var administratorAccount =
                await dbContext.UserAccounts
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == administrator.UserId);

            var administratorPersonId =
                administratorAccount.PersonId;

            var accessService =
                new UserAccessService(dbContext);

            var administratorAccess =
                await accessService.GetAsync(
                    administrator.UserId);

            Assert.NotNull(administratorAccess);
            Assert.Equal(
                SystemRoles.AdministratorCode,
                administratorAccess!.RoleCode);

            AssertPermission(
                administratorAccess,
                SystemPermissions.UsersView);
            AssertPermission(
                administratorAccess,
                SystemPermissions.UsersCreate);
            AssertPermission(
                administratorAccess,
                SystemPermissions.UsersEdit);
            AssertPermission(
                administratorAccess,
                SystemPermissions.UsersDisable);
            AssertPermission(
                administratorAccess,
                SystemPermissions.UsersAssignRole);
            AssertPermission(
                administratorAccess,
                SystemPermissions.RolesView);
            AssertPermission(
                administratorAccess,
                SystemPermissions.RolesEdit);
            AssertPermission(
                administratorAccess,
                SystemPermissions.ProfileViewAll);
            AssertPermission(
                administratorAccess,
                SystemPermissions.ProfileEditAll);

            var userManagement =
                new UserManagementService(
                    dbContext,
                    auditService);

            var userId =
                await userManagement.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Anna",
                        "Domownik",
                        null,
                        null,
                        "anna.account@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N"));

            var userAccount =
                await dbContext.UserAccounts
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == userId);

            var personId =
                userAccount.PersonId;

            var householdAccess =
                await accessService.GetAsync(userId);

            Assert.NotNull(householdAccess);
            Assert.Equal(
                SystemRoles.HouseholdMemberCode,
                householdAccess!.RoleCode);
            Assert.Equal(
                "Anna_Domownik",
                householdAccess.LoginName);

            AssertPermission(
                householdAccess,
                SystemPermissions.ProfileViewOwn,
                PermissionScopes.Own);
            AssertPermission(
                householdAccess,
                SystemPermissions.ProfileEditOwn,
                PermissionScopes.Own);
            AssertNoPermission(
                householdAccess,
                SystemPermissions.UsersView);

            var profileService =
                new ProfileService(
                    dbContext,
                    auditService);

            var ownProfile =
                await profileService.GetAsync(
                    personId,
                    userId);

            Assert.NotNull(ownProfile);
            Assert.True(ownProfile!.CanEdit);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => profileService.GetAsync(
                    administratorPersonId,
                    userId));

            await profileService.UpdateAsync(
                BuildProfileRequest(personId),
                userId,
                Guid.NewGuid().ToString("N"));

            var updatedProfile =
                await profileService.GetAsync(
                    personId,
                    userId);

            Assert.NotNull(updatedProfile);
            Assert.Equal(
                "Anna Testowa",
                updatedProfile!.DisplayName);
            Assert.Equal(
                "500600700",
                updatedProfile.Phone);
            Assert.Equal(
                "Poznań",
                updatedProfile.CorrespondenceCity);

            var profileSeenByAdmin =
                await profileService.GetAsync(
                    personId,
                    administrator.UserId);

            Assert.NotNull(profileSeenByAdmin);
            Assert.True(profileSeenByAdmin!.CanEdit);

            await userManagement.UpdateUserAsync(
                new UpdateUserRequest(
                    userId,
                    "anna.account@example.test",
                    true,
                    SystemRoles.TenantId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var tenantAccess =
                await accessService.GetAsync(userId);

            Assert.NotNull(tenantAccess);
            Assert.Equal(
                SystemRoles.TenantCode,
                tenantAccess!.RoleCode);
            AssertPermission(
                tenantAccess,
                SystemPermissions.ProfileViewOwn,
                PermissionScopes.Own);
            AssertPermission(
                tenantAccess,
                SystemPermissions.ProfileEditOwn,
                PermissionScopes.Own);

            var roleDirectory =
                new RoleDirectoryService(
                    dbContext,
                    auditService);

            await roleDirectory.UpdateRoleAsync(
                new UpdateRoleRequest(
                    SystemRoles.TenantId,
                    "Lokator",
                    "Lokator testowy – konfiguracja regresyjna M02.8.",
                    [
                        new RolePermissionSelection(
                            SystemPermissions.ProfileViewOwn,
                            PermissionScopes.Own),
                        new RolePermissionSelection(
                            SystemPermissions.ProfileEditOwn,
                            PermissionScopes.Own),
                        new RolePermissionSelection(
                            SystemPermissions.UsersView,
                            PermissionScopes.All)
                    ]),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var delegatedAccess =
                await accessService.GetAsync(userId);

            Assert.NotNull(delegatedAccess);
            AssertPermission(
                delegatedAccess!,
                SystemPermissions.UsersView,
                PermissionScopes.All);

            await roleDirectory.ResetRolePermissionsAsync(
                SystemRoles.TenantId,
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var resetAccess =
                await accessService.GetAsync(userId);

            Assert.NotNull(resetAccess);
            AssertNoPermission(
                resetAccess!,
                SystemPermissions.UsersView);
            AssertPermission(
                resetAccess,
                SystemPermissions.ProfileViewOwn,
                PermissionScopes.Own);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => userManagement.UpdateUserAsync(
                    new UpdateUserRequest(
                        administrator.UserId,
                        "admin@example.test",
                        false,
                        SystemRoles.AdministratorId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => userManagement.UpdateUserAsync(
                    new UpdateUserRequest(
                        administrator.UserId,
                        "admin@example.test",
                        true,
                        SystemRoles.HouseholdMemberId),
                    administrator.UserId,
                    Guid.NewGuid().ToString("N")));

            await userManagement.UpdateUserAsync(
                new UpdateUserRequest(
                    userId,
                    "anna.account@example.test",
                    false,
                    SystemRoles.TenantId),
                administrator.UserId,
                Guid.NewGuid().ToString("N"));

            var disabledAccount =
                await dbContext.UserAccounts
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == userId);

            var preservedPerson =
                await dbContext.People
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == personId);

            var preservedProfile =
                await dbContext.PersonProfiles
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.PersonId == personId);

            Assert.False(disabledAccount.IsActive);
            Assert.True(preservedPerson.IsActive);
            Assert.Equal(
                "Anna Testowa",
                preservedPerson.DisplayName);
            Assert.Equal(
                personId,
                preservedProfile.PersonId);

            Assert.Null(
                await accessService.GetAsync(userId));

            var profileAudit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.EventType ==
                            "M02.7.ProfileUpdated");

            var roleAudit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.EventType ==
                            "M02.5.UserRoleChanged");

            var accountAudit =
                await dbContext.AuditLogs
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.EventType ==
                            "M02.7.UserAccountUpdated");

            Assert.True(profileAudit);
            Assert.True(roleAudit);
            Assert.True(accountAudit);
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

    private static UpdatePersonProfileRequest
        BuildProfileRequest(Guid personId) =>
        new(
            personId,
            "Anna",
            "Domownik",
            "Anna Testowa",
            PersonTypes.HouseholdMember,
            "anna.contact@example.test",
            "500600700",
            "Profil regresyjny M02.8",
            new DateTime(1990, 4, 10),
            null,
            "polska",
            null,
            null,
            null,
            null,
            null,
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
            "Testowy",
            "małżonek",
            "501501501",
            "kontakt@example.test",
            null);

    private static void AssertPermission(
        UserAccessSnapshot access,
        string permissionCode,
        string? expectedScope = null)
    {
        var permission =
            access.Permissions.SingleOrDefault(
                x => x.Code == permissionCode);

        Assert.NotNull(permission);

        if (expectedScope is not null)
        {
            Assert.Equal(
                expectedScope,
                permission!.ScopeCode);
        }
    }

    private static void AssertNoPermission(
        UserAccessSnapshot access,
        string permissionCode)
    {
        Assert.DoesNotContain(
            access.Permissions,
            x => x.Code == permissionCode);
    }
}
