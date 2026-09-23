namespace Domio.Application.FamilyFinance;

public interface IFamilyFinanceService
{
    Task<FamilyFinanceOverview> GetOverviewAsync(
        Guid actorUserId,
        Guid? familyGroupId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateGroupAsync(
        CreateFamilyGroupRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<AddFamilyMemberForm?> GetAddMemberFormAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddMemberAsync(
        AddFamilyMemberRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task EndMembershipAsync(
        Guid membershipId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<FamilySharingSnapshot?> GetOwnSharingAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task UpdateOwnSharingAsync(
        UpdateFamilySharingRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateChildIncomeAsync(
        CreateFamilyChildIncomeRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task DeactivateChildIncomeAsync(
        Guid ruleId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<FamilyChildIncomeReceiptForm?> GetChildIncomeReceiptFormAsync(
        Guid familyGroupId,
        Guid ruleId,
        int year,
        int month,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<FamilyChildIncomeReceiptResult> ConfirmChildIncomeReceiptAsync(
        ConfirmFamilyChildIncomeReceiptRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);


    Task<CreateFamilySharedAccountForm?> GetCreateSharedAccountFormAsync(
        Guid familyGroupId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateSharedAccountAsync(
        CreateFamilySharedAccountRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilySharedAccountSummary>> GetSharedAccountsForUserAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<FamilySharedAccountOperationForm?> GetSharedAccountOperationFormAsync(
        Guid sharedAccountId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> PostSharedAccountOperationAsync(
        PostFamilySharedAccountOperationRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

}
