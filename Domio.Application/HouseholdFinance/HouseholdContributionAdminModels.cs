namespace Domio.Application.HouseholdFinance;

public sealed record HouseholdContributionAdminRuleItem(
    Guid RuleId,
    Guid HouseholdMemberId,
    string HouseholdMemberName,
    string ModeCode,
    string ModeNamePl,
    decimal? FixedAmount,
    decimal? Percentage,
    string TargetAccountName,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    int DueOffsetDays,
    int ReminderDays,
    bool IsChildContribution);

public sealed record HouseholdChildMemberItem(
    Guid HouseholdMemberId,
    Guid PersonId,
    string DisplayName,
    Guid? ActiveContributionRuleId,
    decimal? ActiveContributionAmount,
    int? ActiveContributionDueDay,
    string? ActiveContributionTargetAccountName);

public sealed record HouseholdContributionAdminOverview(
    Guid HouseholdId,
    string HouseholdName,
    string CurrencyCode,
    IReadOnlyList<HouseholdContributionAdminRuleItem> ActiveRules,
    IReadOnlyList<HouseholdChildMemberItem> Children);

public sealed record HouseholdContributionTargetAccountOption(
    Guid AccountId,
    string Name,
    string CurrencyCode);

public sealed record HouseholdContributionRuleEditData(
    Guid RuleId,
    string HouseholdMemberName,
    string ModeCode,
    decimal? FixedAmount,
    decimal? Percentage,
    int DueOffsetDays,
    Guid TargetHouseholdAccountId,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    int ReminderDays,
    string? IncomeRuleName,
    decimal? PlannedIncomeAmount,
    string CurrencyCode,
    bool IsChildContribution,
    IReadOnlyList<HouseholdContributionTargetAccountOption> TargetAccounts);

public sealed record UpdateHouseholdContributionRuleRequest(
    Guid RuleId,
    string ModeCode,
    decimal? FixedAmount,
    decimal? Percentage,
    int DueOffsetDays,
    Guid TargetHouseholdAccountId,
    DateTime? ValidToUtc,
    int ReminderDays);

public sealed record UpdateHouseholdContributionRuleResult(
    Guid OldRuleId,
    Guid NewRuleId,
    DateTime EffectiveFromUtc,
    int ObligationsRecalculated,
    int ObligationsCancelled);

public sealed record CreateHouseholdChildRequest(
    string FirstName,
    string LastName,
    string? DisplayName);

public sealed record HouseholdChildContributionFormData(
    Guid HouseholdMemberId,
    string ChildName,
    string CurrencyCode,
    IReadOnlyList<HouseholdContributionTargetAccountOption> TargetAccounts);

public sealed record CreateHouseholdChildContributionRequest(
    Guid HouseholdMemberId,
    decimal FixedAmount,
    int DueDay,
    Guid TargetHouseholdAccountId,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    int ReminderDays);
