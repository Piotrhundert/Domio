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

public sealed class M02_5_RolesPermissionsTests
{
    [Fact]
    public async Task Role_permissions_should_be_editable_persisted_detailed_and_effective()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"domio-m02-5-{Guid.NewGuid():N}");

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
                        "anna@example.test",
                        "DomioAnna123",
                        SystemRoles.HouseholdMemberId),
                    admin.UserId,
                    Guid.NewGuid().ToString("N"));

            var roleService =
                new RoleDirectoryService(
                    dbContext,
                    auditService);

            var householdRole =
                await roleService.GetEditDataAsync(
                    SystemRoles.HouseholdMemberId);

            Assert.NotNull(householdRole);
            Assert.True(
                householdRole!.UsesDefaultPermissions);

            var customPermissions =
                new[]
                {
                    new RolePermissionSelection(
                        SystemPermissions.FinancePersonalViewOwn,
                        PermissionScopes.Own),
                    new RolePermissionSelection(
                        SystemPermissions.FinancePersonalManageOwn,
                        PermissionScopes.Own),
                    new RolePermissionSelection(
                        SystemPermissions.PropertyView,
                        PermissionScopes.All),
                    new RolePermissionSelection(
                        SystemPermissions.NotificationsView,
                        PermissionScopes.All)
                };

            await roleService.UpdateRoleAsync(
                new UpdateRoleRequest(
                    SystemRoles.HouseholdMemberId,
                    "Domownik rozszerzony",
                    "Domownik z ręcznie skonfigurowanym zestawem uprawnień do testu M02.5.",
                    customPermissions),
                admin.UserId,
                Guid.NewGuid().ToString("N"));

            var accessService =
                new UserAccessService(dbContext);

            var access =
                await accessService.GetAsync(userId);

            Assert.NotNull(access);
            Assert.Equal(
                "Domownik rozszerzony",
                access!.RoleNamePl);

            Assert.Contains(
                access.Permissions,
                x =>
                    x.Code ==
                    SystemPermissions.PropertyView);

            Assert.DoesNotContain(
                access.Permissions,
                x =>
                    x.Code ==
                    SystemPermissions.FinanceHouseholdView);

            var editedRole =
                await roleService.GetEditDataAsync(
                    SystemRoles.HouseholdMemberId);

            Assert.NotNull(editedRole);
            Assert.False(
                editedRole!.UsesDefaultPermissions);

            var propertyPermission =
                Assert.Single(
                    editedRole.Permissions,
                    x =>
                        x.Code ==
                        SystemPermissions.PropertyView);

            Assert.Equal("M05", propertyPermission.ModuleCode);
            Assert.Equal(
                "Nieruchomości i pokoje",
                propertyPermission.ModuleNamePl);
            Assert.False(
                string.IsNullOrWhiteSpace(
                    propertyPermission.DescriptionPl));
            Assert.True(
                propertyPermission
                    .AllowedScopeCodes.Count >= 2);

            Assert.True(
                await dbContext.AuditLogs
                    .AnyAsync(
                        x =>
                            x.EventType ==
                            "M02.5.RoleUpdated"));

            await roleService.ResetRolePermissionsAsync(
                SystemRoles.HouseholdMemberId,
                admin.UserId,
                Guid.NewGuid().ToString("N"));

            var resetAccess =
                await accessService.GetAsync(userId);

            Assert.NotNull(resetAccess);
            Assert.Contains(
                resetAccess!.Permissions,
                x =>
                    x.Code ==
                    SystemPermissions.FinanceHouseholdView);

            var administratorRole =
                await roleService.GetEditDataAsync(
                    SystemRoles.AdministratorId);

            Assert.NotNull(administratorRole);

            var withoutProtected =
                administratorRole!.Permissions
                    .Where(x =>
                        x.IsAssigned &&
                        x.Code !=
                            SystemPermissions.RolesEdit)
                    .Select(x =>
                        new RolePermissionSelection(
                            x.Code,
                            x.ScopeCode))
                    .ToArray();

            var protection =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => roleService.UpdateRoleAsync(
                        new UpdateRoleRequest(
                            SystemRoles.AdministratorId,
                            administratorRole.NamePl,
                            administratorRole.DescriptionPl,
                            withoutProtected),
                        admin.UserId,
                        Guid.NewGuid().ToString("N")));

            Assert.Contains(
                "podstawowe uprawnienia bezpieczeństwa",
                protection.Message);

            var schemaVersion =
                await dbContext.SchemaVersions
                    .AsNoTracking()
                    .Where(x => x.Id == 1)
                    .Select(x => x.Version)
                    .SingleAsync();

            Assert.Equal(6, schemaVersion);
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
