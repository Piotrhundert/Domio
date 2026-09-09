using System.Text.Json;
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
            return SystemRolePermissionMatrix.ForRole(roleCode);
        }

        try
        {
            var grants =
                JsonSerializer.Deserialize<List<RolePermissionGrant>>(
                    configurationJson);

            return grants is null
                ? Array.Empty<RolePermissionGrant>()
                : grants;
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
