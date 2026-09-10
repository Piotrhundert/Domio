namespace Domio.Application.HouseholdFinance;

public sealed record HouseholdMemberOption(
    Guid HouseholdMemberId,
    Guid PersonId,
    string DisplayName);

public sealed record HouseholdContributionRoleOption(
    int RoleDefinitionId,
    string RoleCode,
    string RoleNamePl,
    int UserCount);

public sealed record HouseholdMemberCandidate(
    Guid PersonId,
    string DisplayName,
    string LoginName);

public sealed record HouseholdContributionIncomeRuleOption(
    Guid IncomeRuleId,
    Guid HouseholdMemberId,
    string RuleName,
    decimal PlannedAmount,
    string CurrencyCode,
    int PlannedPayDay);

public sealed record HouseholdContributionRuleSummary(
    Guid RuleId,
    Guid HouseholdMemberId,
    string HouseholdMemberName,
    string ModeCode,
    string ModeNamePl,
    decimal? FixedAmount,
    decimal? Percentage,
    Guid? IncomeRuleId,
    string? IncomeRuleName,
    decimal? PlannedIncomeAmount,
    int? PlannedPayDay,
    int DueOffsetDays,
    Guid TargetHouseholdAccountId,
    string TargetHouseholdAccountName,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    int ReminderDays,
    bool IsActive);

public sealed record HouseholdContributionObligationItem(
    Guid ObligationId,
    Guid ContributionRuleId,
    Guid HouseholdMemberId,
    string HouseholdMemberName,
    string PeriodKey,
    decimal Amount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DateTime DueDateUtc,
    string StatusCode,
    string StatusNamePl,
    string ModeCode,
    decimal? Percentage,
    decimal? PlannedIncomeAmount,
    Guid TargetHouseholdAccountId,
    string TargetHouseholdAccountName,
    string CurrencyCode);

public sealed record HouseholdContributionOverview(
    Guid HouseholdId,
    string HouseholdName,
    string CurrencyCode,
    bool CanManage,
    IReadOnlyList<HouseholdContributionRoleOption> Roles,
    IReadOnlyList<HouseholdMemberOption> Members,
    IReadOnlyList<HouseholdMemberCandidate> MemberCandidates,
    IReadOnlyList<HouseholdContributionIncomeRuleOption> IncomeRules,
    IReadOnlyList<HouseholdAccountSummary> TargetAccounts,
    IReadOnlyList<HouseholdContributionRuleSummary> Rules,
    IReadOnlyList<HouseholdContributionObligationItem> Obligations);

public sealed record AddHouseholdMemberRequest(
    Guid PersonId);

public sealed record CreateHouseholdContributionRuleRequest(
    int RoleDefinitionId,
    string ModeCode,
    decimal? FixedAmount,
    decimal? Percentage,
    int DueOffsetDays,
    Guid TargetHouseholdAccountId,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    int ReminderDays);

public sealed record HouseholdContributionBatchResult(
    int RoleDefinitionId,
    string RoleNamePl,
    int UsersMatched,
    int RulesCreated,
    int RulesWaitingForIncomePlan,
    IReadOnlyList<Guid> RuleIds);
