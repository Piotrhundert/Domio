using Domio.Domain.FamilyFinance;
using Domio.Domain.Users;

namespace Domio.Domain.Tests;

public sealed class M04_8_1_FamilyFinancePermissionTests
{
    [Theory]
    [InlineData(FamilyRoles.Adult, "Dorosły")]
    [InlineData(FamilyRoles.Child, "Dziecko")]
    public void Family_role_should_have_polish_name(
        string code,
        string expectedName)
    {
        Assert.True(FamilyRoles.IsValid(code));
        Assert.Equal(expectedName, FamilyRoles.GetNamePl(code));
    }

    [Fact]
    public void Household_member_should_receive_family_permissions()
    {
        var grants =
            FamilyFinancePermissions.AppendDefaults(
                SystemRoles.HouseholdMemberCode,
                []);

        Assert.Contains(
            grants,
            x =>
                x.PermissionCode == FamilyFinancePermissions.View &&
                x.ScopeCode == FamilyFinancePermissionScopes.Membership);

        Assert.Contains(
            grants,
            x =>
                x.PermissionCode == FamilyFinancePermissions.Manage &&
                x.ScopeCode == FamilyFinancePermissionScopes.Membership);

        Assert.Contains(
            grants,
            x =>
                x.PermissionCode == FamilyFinancePermissions.ShareOwn &&
                x.ScopeCode == PermissionScopes.Own);
    }

    [Fact]
    public void Tenant_should_not_receive_family_permissions_by_default()
    {
        var grants =
            FamilyFinancePermissions.AppendDefaults(
                SystemRoles.TenantCode,
                []);

        Assert.DoesNotContain(
            grants,
            x => x.PermissionCode.StartsWith(
                "FamilyFinance.",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Appending_defaults_should_be_idempotent()
    {
        var once =
            FamilyFinancePermissions.AppendDefaults(
                SystemRoles.AdministratorCode,
                []);

        var twice =
            FamilyFinancePermissions.AppendDefaults(
                SystemRoles.AdministratorCode,
                once);

        Assert.Equal(
            once.Count,
            twice.Count);

        Assert.Equal(
            once.Select(x => (x.PermissionCode, x.ScopeCode)),
            twice.Select(x => (x.PermissionCode, x.ScopeCode)));
    }
}
