namespace Domio.Application.FamilyFinance;

public sealed record FamilyAreaSummary(
    Guid AreaId,
    string Name,
    string? SystemCode,
    string Description,
    string Badge,
    int SortOrder,
    bool IsSystem,
    bool IsActive,
    int ItemsCount,
    decimal PlannedAmount,
    decimal ActualAmount);

public sealed record FamilyAreaItemSummary(
    Guid RuleId,
    string Name,
    string CategoryCode,
    string CategoryNamePl,
    decimal PlannedAmount,
    string FrequencyCode,
    string FrequencyNamePl,
    int DueDay,
    string DueDateDescription,
    Guid? BeneficiaryPersonId,
    string? BeneficiaryDisplayName,
    DateTime ActiveFromUtc,
    DateTime? ActiveToUtc,
    bool IsActive,
    Guid? OccurrenceId,
    DateTime? PlannedDateUtc,
    string? OccurrenceStatusCode,
    string OccurrenceStatusNamePl,
    decimal ActualPaidAmount,
    bool CanPay);

public sealed record FamilyAreaContributionSummary(
    Guid ObligationId,
    Guid PersonId,
    string PersonDisplayName,
    string FamilyRoleCode,
    string FamilyRoleNamePl,
    string PeriodKey,
    decimal Amount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateTime DueDateUtc,
    string StatusCode,
    string StatusNamePl);

public sealed record FamilyAreaOverview(
    Guid FamilyGroupId,
    string FamilyGroupName,
    int Year,
    int Month,
    bool CanManage,
    IReadOnlyList<FamilyAreaSummary> Areas);

public sealed record FamilyAreaDetails(
    Guid FamilyGroupId,
    string FamilyGroupName,
    int Year,
    int Month,
    bool CanManage,
    FamilyAreaSummary Area,
    IReadOnlyList<FamilyAreaItemSummary> Items,
    IReadOnlyList<FamilyAreaContributionSummary> Contributions);

public sealed record FamilyAreaBeneficiary(
    Guid PersonId,
    string DisplayName,
    string FamilyRoleCode);

public sealed record CreateFamilyAreaItemContext(
    Guid FamilyGroupId,
    string FamilyGroupName,
    Guid AreaId,
    string AreaName,
    IReadOnlyList<FamilyAreaBeneficiary> Beneficiaries);

public sealed record CreateFamilyAreaRequest(
    Guid FamilyGroupId,
    string Name);

public sealed record CreateFamilyAreaItemRequest(
    Guid AreaId,
    string Name,
    string CategoryCode,
    decimal PlannedAmount,
    string FrequencyCode,
    string DueDateModeCode,
    int DueDay,
    int? DaysBeforeEnd,
    Guid? BeneficiaryPersonId,
    DateTime ActiveFromUtc,
    DateTime? ActiveToUtc);
