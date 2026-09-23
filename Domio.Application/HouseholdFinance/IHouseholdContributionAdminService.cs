namespace Domio.Application.HouseholdFinance;

public interface IHouseholdContributionAdminService
{
    Task<HouseholdContributionAdminOverview?> GetOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<HouseholdContributionRuleEditData?> GetRuleForEditAsync(
        Guid ruleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<UpdateHouseholdContributionRuleResult> UpdateRuleAsync(
        UpdateHouseholdContributionRuleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateChildAsync(
        CreateHouseholdChildRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdChildContributionFormData?> GetChildContributionFormAsync(
        Guid householdMemberId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateChildContributionAsync(
        CreateHouseholdChildContributionRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
