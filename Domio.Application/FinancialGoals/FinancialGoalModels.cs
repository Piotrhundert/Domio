namespace Domio.Application.FinancialGoals;

public sealed record FinancialGoalScopeContext(
    string ScopeCode,
    string ScopeNamePl,
    string OwnerDisplayName,
    Guid? FamilyGroupId,
    bool CanManage,
    bool CanContribute);

public sealed record FinancialGoalSummary(
    Guid GoalId,
    string Name,
    string CategoryCode,
    string CategoryNamePl,
    decimal TargetAmount,
    decimal SavedAmount,
    decimal RemainingAmount,
    decimal ProgressPercent,
    DateTime? TargetDateUtc,
    string? Notes,
    bool IsActive,
    bool IsCompleted,
    int ContributionsCount,
    DateTime? LastContributionAtUtc);

public sealed record FinancialGoalContributionSummary(
    Guid ContributionId,
    decimal Amount,
    DateTime ContributedAtUtc,
    string? Note,
    DateTime CreatedAtUtc,
    Guid? ContributorPersonId,
    string ContributorDisplayName);

public sealed record FinancialGoalOverview(
    FinancialGoalScopeContext Scope,
    IReadOnlyList<FinancialGoalSummary> Goals,
    decimal ActiveTargetTotal,
    decimal ActiveSavedTotal,
    decimal ActiveRemainingTotal);

public sealed record FinancialGoalDetails(
    FinancialGoalScopeContext Scope,
    FinancialGoalSummary Goal,
    IReadOnlyList<FinancialGoalContributionSummary> Contributions);

public sealed record CreateFinancialGoalRequest(
    string ScopeCode,
    Guid? FamilyGroupId,
    string Name,
    string CategoryCode,
    decimal TargetAmount,
    DateTime? TargetDateUtc,
    string? Notes,
    decimal? InitialAmount);

public sealed record UpdateFinancialGoalRequest(
    Guid GoalId,
    string Name,
    string CategoryCode,
    decimal TargetAmount,
    DateTime? TargetDateUtc,
    string? Notes);

public sealed record AddFinancialGoalContributionRequest(
    Guid GoalId,
    decimal Amount,
    DateTime ContributedAtUtc,
    string? Note);
