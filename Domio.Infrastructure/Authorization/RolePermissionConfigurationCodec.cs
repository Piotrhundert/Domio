using System.Text.Json;
using Domio.Domain.FamilyFinance;
using Domio.Domain.Users;

namespace Domio.Infrastructure.Authorization;

internal static class RolePermissionConfigurationCodec
{
    public static IReadOnlyList<RolePermissionGrant> Resolve(
        string roleCode,
        string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return FamilyFinancePermissions.AppendDefaults(
                roleCode,
                SystemRolePermissionMatrix.ForRole(roleCode));
        }

        try
        {
            var grants =
                JsonSerializer.Deserialize<List<RolePermissionGrant>>(
                    configurationJson);

            if (grants is null)
            {
                return Array.Empty<RolePermissionGrant>();
            }

            // M04.8.1: istniejące role zapisane przed dodaniem modułu
            // rodzinnego nie posiadają jeszcze kodów FamilyFinance.* w JSON.
            // Dopinamy bezpieczne wartości domyślne dla ról systemowych,
            // zachowując dotychczasową konfigurację pozostałych uprawnień.
            return FamilyFinancePermissions.AppendDefaults(
                roleCode,
                grants);
        }
        catch (JsonException)
        {
            // Fail closed: uszkodzona konfiguracja nie może przyznać praw.
            return Array.Empty<RolePermissionGrant>();
        }
    }

    public static string Serialize(
        IEnumerable<RolePermissionGrant> grants) =>
        JsonSerializer.Serialize(
            grants
                .OrderBy(x => x.PermissionCode)
                .ThenBy(x => x.ScopeCode)
                .ToArray());
}
