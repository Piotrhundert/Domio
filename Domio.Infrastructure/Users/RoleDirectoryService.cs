using System.Text.Json;
using Domio.Application.Auditing;
using Domio.Application.Users;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Users;

public sealed class RoleDirectoryService(
    DomioDbContext dbContext,
    IAuditService auditService) : IRoleDirectoryService
{
    public async Task<RolePermissionsOverview> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var roleRows = await dbContext.RoleDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(role => new
            {
                role.Id,
                role.Code,
                role.NamePl,
                role.DescriptionPl,
                role.IsSystem,
                role.PermissionConfigurationJson,
                AssignedUsers =
                    dbContext.UserAccounts.Count(
                        account =>
                            account.RoleDefinitionId == role.Id)
            })
            .ToListAsync(cancellationToken);

        var descriptorLookup =
            SystemPermissions.All.ToDictionary(
                x => x.Code,
                StringComparer.Ordinal);

        var roles = roleRows
            .Select(role =>
            {
                var permissions =
                    RolePermissionConfigurationCodec
                        .Resolve(
                            role.Code,
                            role.PermissionConfigurationJson)
                        .Where(grant =>
                            descriptorLookup.ContainsKey(
                                grant.PermissionCode))
                        .Select(grant =>
                        {
                            var permission =
                                descriptorLookup[
                                    grant.PermissionCode];

                            return ToDirectoryItem(
                                permission,
                                grant.ScopeCode);
                        })
                        .OrderBy(x => x.ModuleCode)
                        .ThenBy(x => x.Code)
                        .ToArray();

                return new RoleWithPermissionsItem(
                    role.Id,
                    role.Code,
                    role.NamePl,
                    role.DescriptionPl,
                    role.IsSystem,
                    string.IsNullOrWhiteSpace(
                        role.PermissionConfigurationJson),
                    role.AssignedUsers,
                    permissions);
            })
            .ToArray();

        var catalog =
            SystemPermissions.All
                .OrderBy(x => x.ModuleCode)
                .ThenBy(x => x.Code)
                .Select(x =>
                    new PermissionDirectoryItem(
                        x.Code,
                        x.ModuleCode,
                        x.ModuleNamePl,
                        x.NamePl,
                        x.DescriptionPl,
                        x.RiskLevel,
                        x.AllowedScopes))
                .ToArray();

        return new RolePermissionsOverview(
            roles,
            catalog);
    }

    public async Task<RoleEditData?> GetEditDataAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var role = await dbContext.RoleDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == roleId,
                cancellationToken);

        if (role is null)
        {
            return null;
        }

        var currentGrants =
            RolePermissionConfigurationCodec
                .Resolve(
                    role.Code,
                    role.PermissionConfigurationJson)
                .ToDictionary(
                    x => x.PermissionCode,
                    x => x.ScopeCode,
                    StringComparer.Ordinal);

        var protectedCodes =
            role.Code == SystemRoles.AdministratorCode
                ? SystemPermissions
                    .AdministratorProtectedPermissions
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(
                    StringComparer.Ordinal);

        var permissions =
            SystemPermissions.All
                .OrderBy(x => x.ModuleCode)
                .ThenBy(x => x.Code)
                .Select(permission =>
                {
                    var assigned =
                        currentGrants.TryGetValue(
                            permission.Code,
                            out var scopeCode);

                    var selectedScope =
                        assigned &&
                        permission.AllowedScopes.Contains(
                            scopeCode!,
                            StringComparer.Ordinal)
                            ? scopeCode!
                            : permission.AllowedScopes[0];

                    return new RoleEditPermissionData(
                        permission.Code,
                        permission.ModuleCode,
                        permission.ModuleNamePl,
                        permission.NamePl,
                        permission.DescriptionPl,
                        permission.RiskLevel,
                        assigned,
                        selectedScope,
                        permission.AllowedScopes,
                        protectedCodes.Contains(
                            permission.Code));
                })
                .ToArray();

        return new RoleEditData(
            role.Id,
            role.Code,
            role.NamePl,
            role.DescriptionPl,
            role.IsSystem,
            string.IsNullOrWhiteSpace(
                role.PermissionConfigurationJson),
            permissions);
    }

    public async Task UpdateRoleAsync(
        UpdateRoleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.RolesEdit,
            cancellationToken);

        ValidateRoleText(
            request.NamePl,
            "Nazwa roli",
            100);

        ValidateRoleText(
            request.DescriptionPl,
            "Opis roli",
            1000);

        var role = await dbContext.RoleDefinitions
            .SingleOrDefaultAsync(
                x => x.Id == request.RoleId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Rola nie istnieje.");

        var duplicatePermission =
            request.Permissions
                .GroupBy(
                    x => x.PermissionCode,
                    StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePermission is not null)
        {
            throw new InvalidOperationException(
                "Lista uprawnień zawiera duplikaty.");
        }

        var selections =
            request.Permissions.ToArray();

        foreach (var selection in selections)
        {
            var descriptor =
                SystemPermissions.Find(
                    selection.PermissionCode)
                ?? throw new InvalidOperationException(
                    $"Nieznane uprawnienie: {selection.PermissionCode}.");

            if (!descriptor.AllowedScopes.Contains(
                    selection.ScopeCode,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Niedozwolony zakres dla {selection.PermissionCode}.");
            }
        }

        if (role.Code == SystemRoles.AdministratorCode)
        {
            var selectedCodes =
                selections
                    .Select(x => x.PermissionCode)
                    .ToHashSet(StringComparer.Ordinal);

            var missingProtected =
                SystemPermissions
                    .AdministratorProtectedPermissions
                    .Where(x => !selectedCodes.Contains(x))
                    .ToArray();

            if (missingProtected.Length > 0)
            {
                throw new InvalidOperationException(
                    "Rola Administrator musi zachować podstawowe uprawnienia bezpieczeństwa do zarządzania użytkownikami i rolami.");
            }
        }

        var oldGrants =
            RolePermissionConfigurationCodec.Resolve(
                role.Code,
                role.PermissionConfigurationJson);

        var oldValues =
            JsonSerializer.Serialize(new
            {
                role.NamePl,
                role.DescriptionPl,
                UsesDefaultPermissions =
                    string.IsNullOrWhiteSpace(
                        role.PermissionConfigurationJson),
                Permissions = oldGrants
            });

        var newGrants =
            selections
                .Select(x =>
                    new RolePermissionGrant(
                        x.PermissionCode,
                        x.ScopeCode))
                .OrderBy(x => x.PermissionCode)
                .ToArray();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        role.NamePl = request.NamePl.Trim();
        role.DescriptionPl = request.DescriptionPl.Trim();
        role.PermissionConfigurationJson =
            RolePermissionConfigurationCodec.Serialize(
                newGrants);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.5.RoleUpdated",
                EntityType: "RoleDefinition",
                EntityId: role.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Administrator zmienił nazwę, opis lub konfigurację uprawnień roli.",
                OldValuesJson: oldValues,
                NewValuesJson: JsonSerializer.Serialize(new
                {
                    role.NamePl,
                    role.DescriptionPl,
                    UsesDefaultPermissions = false,
                    Permissions = newGrants
                })),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    public async Task ResetRolePermissionsAsync(
        int roleId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await PermissionEnforcement.EnsureUserHasAsync(
            dbContext,
            actorUserId,
            SystemPermissions.RolesEdit,
            cancellationToken);

        var role = await dbContext.RoleDefinitions
            .SingleOrDefaultAsync(
                x => x.Id == roleId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Rola nie istnieje.");

        var oldJson =
            role.PermissionConfigurationJson;

        role.PermissionConfigurationJson = null;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.5.RolePermissionsReset",
                EntityType: "RoleDefinition",
                EntityId: role.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Przywrócono domyślną macierz uprawnień roli.",
                OldValuesJson: oldJson,
                NewValuesJson:
                    JsonSerializer.Serialize(
                        SystemRolePermissionMatrix
                            .ForRole(role.Code))),
            cancellationToken);
    }

    private static RolePermissionDirectoryItem
        ToDirectoryItem(
            PermissionDescriptor permission,
            string scopeCode) =>
        new(
            permission.Code,
            permission.ModuleCode,
            permission.ModuleNamePl,
            permission.NamePl,
            permission.DescriptionPl,
            permission.RiskLevel,
            scopeCode,
            PermissionScopes.GetNamePl(
                scopeCode));

    private static void ValidateRoleText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} nie może być pusta.");
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }
    }
}
