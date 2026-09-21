using Domio.Domain.Users;

namespace Domio.Domain.FamilyFinance;

public static class FamilyFinancePermissionScopes
{
    public const string Membership = PermissionScopes.FamilyMembership;
}

public static class FamilyFinancePermissions
{
    public const string View = "FamilyFinance.View";
    public const string Manage = "FamilyFinance.Manage";
    public const string ShareOwn = "FamilyFinance.ShareOwn";

    public static readonly IReadOnlyList<string> AllCodes =
    [
        View,
        Manage,
        ShareOwn
    ];

    public static IReadOnlyList<RolePermissionGrant> DefaultsForRole(
        string roleCode) =>
        roleCode switch
        {
            SystemRoles.AdministratorCode =>
            [
                new(View, FamilyFinancePermissionScopes.Membership),
                new(Manage, FamilyFinancePermissionScopes.Membership),
                new(ShareOwn, PermissionScopes.Own)
            ],
            SystemRoles.HouseholdMemberCode =>
            [
                new(View, FamilyFinancePermissionScopes.Membership),
                new(Manage, FamilyFinancePermissionScopes.Membership),
                new(ShareOwn, PermissionScopes.Own)
            ],
            _ => Array.Empty<RolePermissionGrant>()
        };

    public static IReadOnlyList<RolePermissionGrant> AppendDefaults(
        string roleCode,
        IEnumerable<RolePermissionGrant> currentGrants)
    {
        var result = currentGrants.ToList();

        foreach (var grant in DefaultsForRole(roleCode))
        {
            if (result.Any(x =>
                    string.Equals(
                        x.PermissionCode,
                        grant.PermissionCode,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            result.Add(grant);
        }

        return result
            .OrderBy(x => x.PermissionCode)
            .ThenBy(x => x.ScopeCode)
            .ToArray();
    }
}
