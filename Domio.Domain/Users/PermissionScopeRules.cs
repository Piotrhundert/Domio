namespace Domio.Domain.Users;

public sealed record PermissionResourceAccess(
    Guid? OwnerPersonId = null,
    bool IsOwnRoom = false,
    bool IsOwnAgreement = false,
    bool IsCoveredByAgreement = false);

public static class PermissionScopeRules
{
    public static bool IsAllowed(
        string scopeCode,
        Guid currentPersonId,
        PermissionResourceAccess resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        return scopeCode switch
        {
            PermissionScopes.All => true,

            PermissionScopes.Own =>
                resource.OwnerPersonId.HasValue &&
                resource.OwnerPersonId.Value ==
                    currentPersonId,

            PermissionScopes.OwnRoom =>
                resource.IsOwnRoom,

            PermissionScopes.OwnAgreement =>
                resource.IsOwnAgreement,

            PermissionScopes.Agreement =>
                resource.IsCoveredByAgreement,

            _ => false
        };
    }
}
