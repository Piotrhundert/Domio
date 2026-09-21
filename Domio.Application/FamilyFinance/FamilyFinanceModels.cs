namespace Domio.Application.FamilyFinance;

public sealed record FamilyGroupChoice(
    Guid FamilyGroupId,
    string Name,
    string CurrentMemberRoleCode,
    string CurrentMemberRoleNamePl);

public sealed record FamilyMemberBudgetSummary(
    Guid MembershipId,
    Guid PersonId,
    string DisplayName,
    string FamilyRoleCode,
    string FamilyRoleNamePl,
    DateTime ValidFromUtc,
    bool SharePlannedIncome,
    bool ShareActualIncome,
    bool ShareFamilyExpenses,
    bool ShareRecurringRules,
    decimal? PlannedIncome,
    decimal? ActualIncome);

public sealed record FamilyChildIncomeRuleSummary(
    Guid RuleId,
    Guid BeneficiaryPersonId,
    string BeneficiaryDisplayName,
    string IncomeKindCode,
    string IncomeKindNamePl,
    string Name,
    decimal PlannedAmount,
    string FrequencyCode,
    string FrequencyNamePl,
    int DueDay,
    DateTime ActiveFromUtc,
    DateTime? ActiveToUtc,
    bool IsActive,
    bool AppliesInSelectedMonth);

public sealed record FamilySharingSnapshot(
    Guid FamilyGroupId,
    string FamilyGroupName,
    Guid PersonId,
    string PersonDisplayName,
    bool SharePlannedIncome,
    bool ShareActualIncome,
    bool ShareFamilyExpenses,
    bool ShareRecurringRules,
    DateTime EffectiveFromUtc);

public sealed record FamilyFinanceOverview(
    Guid? HouseholdId,
    string? HouseholdName,
    Guid CurrentPersonId,
    int Year,
    int Month,
    IReadOnlyList<FamilyGroupChoice> Groups,
    Guid? SelectedFamilyGroupId,
    string? SelectedFamilyGroupName,
    string? CurrentMemberRoleCode,
    IReadOnlyList<FamilyMemberBudgetSummary> Members,
    decimal PlannedIncomeTotal,
    decimal ActualIncomeTotal,
    IReadOnlyList<FamilyChildIncomeRuleSummary> ChildIncomeRules,
    FamilySharingSnapshot? OwnSharing)
{
    public bool HasHousehold => HouseholdId.HasValue;
    public bool HasFamily => SelectedFamilyGroupId.HasValue;
}

public sealed record CreateFamilyGroupRequest(
    string Name);

public sealed record FamilyMemberCandidate(
    Guid PersonId,
    string DisplayName);

public sealed record AddFamilyMemberForm(
    Guid FamilyGroupId,
    string FamilyGroupName,
    IReadOnlyList<FamilyMemberCandidate> Candidates);

public sealed record AddFamilyMemberRequest(
    Guid FamilyGroupId,
    Guid PersonId,
    string FamilyRoleCode);

public sealed record UpdateFamilySharingRequest(
    Guid FamilyGroupId,
    bool SharePlannedIncome,
    bool ShareActualIncome,
    bool ShareFamilyExpenses,
    bool ShareRecurringRules);


public sealed record CreateFamilyChildIncomeRequest(
    Guid FamilyGroupId,
    Guid BeneficiaryPersonId,
    string IncomeKindCode,
    string? CustomName,
    decimal PlannedAmount,
    string FrequencyCode,
    int DueDay,
    DateTime ActiveFromUtc,
    DateTime? ActiveToUtc);
