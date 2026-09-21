namespace Domio.Application.FamilyFinance;

public sealed record FamilyBudgetCategorySummary(
    string CategoryCode,
    string CategoryNamePl,
    decimal PlannedAmount,
    decimal ActualAmount);

public sealed record FamilyPersonExpenseSummary(
    Guid PersonId,
    decimal PlannedAmount,
    decimal ActualAmount);

public sealed record FamilyChildCostSummary(
    Guid PersonId,
    string DisplayName,
    decimal PlannedAmount,
    decimal ActualAmount);

public sealed record FamilyRecurringCostSummary(
    Guid RuleId,
    string Name,
    string CategoryCode,
    string CategoryNamePl,
    decimal PlannedAmount,
    string FrequencyCode,
    string FrequencyNamePl,
    int DueDay,
    Guid? BeneficiaryPersonId,
    string? BeneficiaryDisplayName,
    DateTime ActiveFromUtc,
    DateTime? ActiveToUtc,
    bool IsActive);

public sealed record FamilyBudgetLinkSummary(
    Guid LinkId,
    string SourceType,
    string SourceTypeNamePl,
    Guid SourceId,
    string SourceLabel,
    string? PeriodKey,
    DateTime? SourceDateUtc,
    decimal? Amount,
    string CategoryCode,
    string CategoryNamePl,
    Guid? PersonId,
    string? PersonDisplayName,
    Guid? BeneficiaryPersonId,
    string? BeneficiaryDisplayName);

public sealed record FamilyBudgetOverview(
    Guid FamilyGroupId,
    int Year,
    int Month,
    decimal PlannedExpenseTotal,
    decimal ActualExpenseTotal,
    IReadOnlyList<FamilyBudgetCategorySummary> Categories,
    IReadOnlyList<FamilyPersonExpenseSummary> PersonExpenses,
    IReadOnlyList<FamilyChildCostSummary> ChildCosts,
    IReadOnlyList<FamilyRecurringCostSummary> FamilyRules,
    IReadOnlyList<FamilyBudgetLinkSummary> ActualExpenseLinks);

public sealed record CreateFamilyRecurringExpenseRequest(
    Guid FamilyGroupId,
    string Name,
    string CategoryCode,
    decimal PlannedAmount,
    string FrequencyCode,
    int DueDay,
    Guid? BeneficiaryPersonId,
    DateTime ActiveFromUtc,
    DateTime? ActiveToUtc);

public sealed record FamilyBudgetCandidate(
    string SourceType,
    Guid SourceId,
    string SourceTypeNamePl,
    string DisplayName,
    DateTime? SourceDateUtc,
    decimal PlannedOrActualAmount,
    string CurrencyCode,
    bool IsRecurring);

public sealed record FamilyBudgetBeneficiary(
    Guid PersonId,
    string DisplayName,
    string FamilyRoleCode);

public sealed record FamilyBudgetLinkForm(
    Guid FamilyGroupId,
    string FamilyGroupName,
    int Year,
    int Month,
    IReadOnlyList<FamilyBudgetCandidate> Candidates,
    IReadOnlyList<FamilyBudgetBeneficiary> Beneficiaries,
    IReadOnlyList<FamilyBudgetLinkSummary> ExistingLinks,
    bool CanLinkOwnPersonalExpenses,
    bool CanLinkOwnRecurringRules,
    bool CanLinkHouseholdExpenses);

public sealed record CreateFamilyBudgetLinkRequest(
    Guid FamilyGroupId,
    string SourceType,
    Guid SourceId,
    string CategoryCode,
    Guid? BeneficiaryPersonId);
