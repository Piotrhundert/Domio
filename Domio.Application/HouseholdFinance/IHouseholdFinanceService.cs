namespace Domio.Application.HouseholdFinance;

public interface IHouseholdFinanceService
{
    Task<HouseholdFinanceOverview?> GetOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateAccountAsync(
        CreateHouseholdAccountRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> PostOperationAsync(
        PostHouseholdOperationRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdTransferResult> TransferBetweenAccountsAsync(
        CreateHouseholdTransferRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdAccountClosureInfo?> GetAccountClosureInfoAsync(
        Guid accountId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task CloseAccountAsync(
        Guid accountId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdContributionOverview?> GetContributionOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddHouseholdMemberAsync(
        AddHouseholdMemberRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdContributionBatchResult> CreateContributionRuleAsync(
        CreateHouseholdContributionRuleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);


    Task<HouseholdContributionPaymentForm?> GetContributionPaymentFormAsync(
        Guid obligationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> SubmitContributionPaymentAsync(
        SubmitHouseholdContributionPaymentRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task ApproveContributionPaymentAsync(
        ReviewHouseholdContributionPaymentRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task RejectContributionPaymentAsync(
        ReviewHouseholdContributionPaymentRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdInvoiceOverview?> GetInvoiceOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateInvoiceAsync(
        CreateHouseholdInvoiceRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<HouseholdInvoicePaymentForm?> GetInvoicePaymentFormAsync(
        Guid invoiceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> PayInvoiceAsync(
        PayHouseholdInvoiceRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task CancelInvoiceAsync(
        Guid invoiceId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
