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
    bool AppliesInSelectedMonth,
    string PeriodKey,
    DateTime? PlannedDateUtc,
    bool IsReceived,
    decimal? ReceivedAmount,
    DateTime? ReceivedAtUtc,
    bool CanConfirmReceipt);

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

    public IReadOnlyList<FamilySharedAccountSummary> SharedAccounts { get; init; } = [];

    public IReadOnlyList<FamilyChildHouseholdContributionSummary>
        ChildHouseholdContributions { get; init; } = [];
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

public sealed record FamilyIncomeReceiptAccount(
    string AccountType,
    string AccountTypeNamePl,
    Guid AccountId,
    string AccountName,
    string CurrencyCode,
    decimal Balance);

public sealed record FamilyChildIncomeReceiptForm(
    Guid FamilyGroupId,
    string FamilyGroupName,
    Guid RuleId,
    string RuleName,
    string IncomeKindNamePl,
    Guid BeneficiaryPersonId,
    string BeneficiaryDisplayName,
    int Year,
    int Month,
    string PeriodKey,
    decimal Amount,
    DateTime PlannedDateUtc,
    IReadOnlyList<FamilyIncomeReceiptAccount> Accounts);

public sealed record ConfirmFamilyChildIncomeReceiptRequest(
    Guid FamilyGroupId,
    Guid RuleId,
    int Year,
    int Month,
    string AccountType,
    Guid AccountId,
    DateTime ReceivedAtUtc);

public sealed record FamilyChildIncomeReceiptResult(
    Guid ReceiptId,
    string SourceType,
    Guid SourceId);

public sealed record FamilySharedAccountPersonOption(
    Guid PersonId,
    string DisplayName);

public sealed record CreateFamilySharedAccountForm(
    Guid FamilyGroupId,
    string FamilyGroupName,
    IReadOnlyList<FamilySharedAccountPersonOption> AdultMembers);

public sealed record CreateFamilySharedAccountRequest(
    Guid FamilyGroupId,
    string Name,
    string AccountTypeCode,
    decimal InitialBalance,
    Guid OwnerPersonId,
    Guid CoOwnerPersonId);

public sealed record FamilySharedAccountSummary(
    Guid SharedAccountId,
    Guid FamilyGroupId,
    string FamilyGroupName,
    Guid AccountId,
    string AccountName,
    string AccountTypeCode,
    string AccountTypeNamePl,
    string CurrencyCode,
    decimal Balance,
    Guid OwnerPersonId,
    string OwnerDisplayName,
    Guid CoOwnerPersonId,
    string CoOwnerDisplayName,
    string CurrentPersonRoleCode,
    string CurrentPersonRoleNamePl,
    bool IsActive);

public sealed record FamilySharedAccountOperationForm(
    Guid SharedAccountId,
    Guid FamilyGroupId,
    string FamilyGroupName,
    Guid AccountId,
    string AccountName,
    string CurrencyCode,
    decimal Balance,
    string CurrentPersonRoleNamePl);

public sealed record PostFamilySharedAccountOperationRequest(
    Guid SharedAccountId,
    string KindCode,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Description,
    string? CategoryCode,
    string? Counterparty);

public sealed record FamilyChildHouseholdContributionSummary(
    Guid? ObligationId,
    Guid ContributionRuleId,
    Guid ChildPersonId,
    string ChildDisplayName,
    string PeriodKey,
    decimal Amount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DateTime DueDateUtc,
    string StatusCode,
    string StatusNamePl,
    Guid TargetHouseholdAccountId,
    string TargetHouseholdAccountName,
    string CurrencyCode,
    bool CanPay,
    string? AvailabilityNote);

public sealed record FamilyChildContributionPaymentSource(
    string SourceType,
    string SourceTypeNamePl,
    Guid SourceId,
    string AccountName,
    string CurrencyCode,
    decimal Balance,
    bool IsTargetHouseholdAccount);

public sealed record FamilyChildContributionPaymentForm(
    Guid FamilyGroupId,
    string FamilyGroupName,
    Guid ObligationId,
    Guid ChildPersonId,
    string ChildDisplayName,
    string PeriodKey,
    decimal Amount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DateTime DueDateUtc,
    Guid TargetHouseholdAccountId,
    string TargetHouseholdAccountName,
    string CurrencyCode,
    IReadOnlyList<FamilyChildContributionPaymentSource> Sources);

public sealed record PayFamilyChildContributionRequest(
    Guid FamilyGroupId,
    Guid ObligationId,
    string SourceType,
    Guid SourceId,
    DateTime PaidAtUtc);

public sealed record FamilyChildContributionPaymentResult(
    Guid ObligationId,
    string SourceType,
    Guid SourceId,
    Guid? SourceTransactionId,
    Guid? HouseholdEntryId,
    decimal PaidAmount);

