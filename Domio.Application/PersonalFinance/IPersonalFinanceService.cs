namespace Domio.Application.PersonalFinance;

public interface IPersonalFinanceService
{
    Task<PersonalFinanceOverview> GetOwnOverviewAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateOwnAccountAsync(
        CreatePersonalAccountRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> PostOwnOperationAsync(
        PostPersonalOperationRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateOwnRecurringRuleAsync(
        CreatePersonalRecurringRuleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PersonalRecurringRuleSummary?> GetOwnRecurringRuleAsync(
        Guid ruleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task UpdateOwnRecurringRuleAsync(
        UpdatePersonalRecurringRuleRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task DeactivateOwnRecurringRuleAsync(
        Guid ruleId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PersonalRecurringOccurrenceItem?> GetOwnRecurringOccurrenceAsync(
        Guid occurrenceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid> ConfirmOwnRecurringOccurrenceAsync(
        ConfirmPersonalRecurringOccurrenceRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PersonalTransferResult> TransferBetweenOwnAccountsAsync(
        CreatePersonalTransferRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PersonalAccountClosureInfo?> GetOwnAccountClosureInfoAsync(
        Guid accountId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task CloseOwnAccountAsync(
        Guid accountId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PersonalTransactionCorrectionInfo?> GetOwnTransactionCorrectionInfoAsync(
        Guid transactionId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Guid?> CorrectOwnTransactionAsync(
        CorrectPersonalTransactionRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<PersonalTransactionHistoryResult> GetOwnTransactionHistoryAsync(
        PersonalTransactionHistoryFilter filter,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
