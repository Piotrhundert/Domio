namespace Domio.Application.FamilyFinance;

public interface IFamilyBudgetService
{
    Task<FamilyBudgetOverview> GetOverviewAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateRecurringExpenseAsync(
        CreateFamilyRecurringExpenseRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task DeactivateRecurringExpenseAsync(
        Guid ruleId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<FamilyBudgetLinkForm> GetLinkFormAsync(
        Guid familyGroupId,
        Guid actorUserId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<Guid> LinkSourceAsync(
        CreateFamilyBudgetLinkRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task UnlinkSourceAsync(
        Guid linkId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<FamilyExpensePaymentForm?> GetExpensePaymentFormAsync(
        Guid familyGroupId,
        Guid occurrenceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<FamilyExpensePaymentResult> PayExpenseAsync(
        PayFamilyExpenseRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
