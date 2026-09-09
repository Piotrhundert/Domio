using Domio.Application.Authentication;
using Domio.Application.Users;
using Domio.Domain.Users;
using Domio.Infrastructure.Auditing;
using Domio.Infrastructure.Authentication;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Domio.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;

namespace Domio.IntegrationTests;

public sealed class M02_6_RbacPermissionTests
{
    [Fact]
    public async Task PermissionCode_should_be_enforced_in_backend_and_scope_rules_should_fail_closed()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-6-{Guid.NewGuid():N}");

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

            var householdUserId =
                await management.CreateUserAsync(
                    new CreateUserRequest(
                        null,
                        "Anna",
                        "Domownik",
                        null,
                        null,
                        "anna@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    admin.UserId,
                    Guid.NewGuid().ToString("N"));

            var adminGrant =
                await PermissionEnforcement
                    .EnsureUserHasAsync(
                        dbContext,
                        admin.UserId,
                        SystemPermissions.UsersView);

            Assert.Equal(
                PermissionScopes.All,
                adminGrant.ScopeCode);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => PermissionEnforcement
                    .EnsureUserHasAsync(
                        dbContext,
                        householdUserId,
                        SystemPermissions.UsersView));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => management.CreatePersonAsync(
                    new CreatePersonRequest(
                        "Brak",
                        "Uprawnienia",
                        null,
                        null,
                        null),
                    householdUserId,
                    Guid.NewGuid().ToString("N")));

            var roleService =
                new RoleDirectoryService(
                    dbContext,
                    auditService);

            await roleService.UpdateRoleAsync(
                new UpdateRoleRequest(
                    SystemRoles.HouseholdMemberId,
                    "Domownik",
                    "Domownik testowy z delegowanym dostępem do podglądu i tworzenia użytkowników.",
                    [
                        new RolePermissionSelection(
                            SystemPermissions.UsersView,
                            PermissionScopes.All),
                        new RolePermissionSelection(
                            SystemPermissions.UsersCreate,
                            PermissionScopes.All)
                    ]),
                admin.UserId,
                Guid.NewGuid().ToString("N"));

            var delegatedView =
                await PermissionEnforcement
                    .EnsureUserHasAsync(
                        dbContext,
                        householdUserId,
                        SystemPermissions.UsersView);

            Assert.Equal(
                PermissionScopes.All,
                delegatedView.ScopeCode);

            var createdPersonId =
                await management.CreatePersonAsync(
                    new CreatePersonRequest(
                        "Delegowany",
                        "Użytkownik",
                        null,
                        null,
                        null),
                    householdUserId,
                    Guid.NewGuid().ToString("N"));

            Assert.True(
                await dbContext.People
                    .AnyAsync(x =>
                        x.Id == createdPersonId));

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => PermissionEnforcement
                    .EnsureUserHasAsync(
                        dbContext,
                        householdUserId,
                        SystemPermissions.RolesEdit));

            var personId = Guid.NewGuid();
            var otherPersonId = Guid.NewGuid();

            Assert.True(
                PermissionScopeRules.IsAllowed(
                    PermissionScopes.All,
                    personId,
                    new PermissionResourceAccess()));

            Assert.True(
                PermissionScopeRules.IsAllowed(
                    PermissionScopes.Own,
                    personId,
                    new PermissionResourceAccess(
                        OwnerPersonId: personId)));

            Assert.False(
                PermissionScopeRules.IsAllowed(
                    PermissionScopes.Own,
                    personId,
                    new PermissionResourceAccess(
                        OwnerPersonId: otherPersonId)));

            Assert.True(
                PermissionScopeRules.IsAllowed(
                    PermissionScopes.OwnRoom,
                    personId,
                    new PermissionResourceAccess(
                        IsOwnRoom: true)));

            Assert.False(
                PermissionScopeRules.IsAllowed(
                    PermissionScopes.OwnRoom,
                    personId,
                    new PermissionResourceAccess(
                        IsOwnRoom: false)));

            Assert.True(
                PermissionScopeRules.IsAllowed(
                    PermissionScopes.OwnAgreement,
                    personId,
                    new PermissionResourceAccess(
                        IsOwnAgreement: true)));

            Assert.True(
                PermissionScopeRules.IsAllowed(
                    PermissionScopes.Agreement,
                    personId,
                    new PermissionResourceAccess(
                        IsCoveredByAgreement: true)));

            Assert.False(
                PermissionScopeRules.IsAllowed(
                    "UnknownScope",
                    personId,
                    new PermissionResourceAccess(
                        OwnerPersonId: personId,
                        IsOwnRoom: true,
                        IsOwnAgreement: true,
                        IsCoveredByAgreement: true)));

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.True(schemaVersion >= 6);
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
