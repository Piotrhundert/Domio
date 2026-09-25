namespace Domio.Application.FamilyFinance;

public sealed record EditFamilyAreaItemContext(
    Guid FamilyGroupId,
    string FamilyGroupName,
    Guid AreaId,
    string AreaName,
    Guid RuleId,
    string Name,
    string CategoryCode,
    decimal PlannedAmount,
    string FrequencyCode,
    string DueDateModeCode,
    int DueDay,
    int? DaysBeforeEnd,
    Guid? BeneficiaryPersonId,
    DateTime ActiveFromUtc,
    DateTime? ActiveToUtc,
    bool IsActive,
    IReadOnlyList<FamilyAreaBeneficiary> Beneficiaries);

public sealed record UpdateFamilyAreaItemRequest(
    Guid RuleId,
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
