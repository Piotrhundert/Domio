namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdMemberObligation
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid HouseholdMemberId { get; set; }

    public string SourceTypeCode { get; set; } =
        HouseholdMemberObligationSourceTypes.OtherCost;

    public Guid? SourceInvoiceId { get; set; }

    public string Description { get; set; } = string.Empty;

    public long AmountMinor { get; set; }

    public long PaidAmountMinor { get; set; }

    public DateTime DueDateUtc { get; set; }

    public string StatusCode { get; set; } =
        HouseholdMemberObligationStatuses.Pending;

    public Guid TargetHouseholdAccountId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}
