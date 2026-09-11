namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdContributionPaymentRequest
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid HouseholdMemberId { get; set; }

    public Guid ObligationId { get; set; }

    public Guid SourcePersonalAccountId { get; set; }

    public Guid TargetHouseholdAccountId { get; set; }

    public long AmountMinor { get; set; }

    public string StatusCode { get; set; } =
        HouseholdContributionPaymentStatuses.Pending;

    public Guid SubmittedByUserId { get; set; }

    public DateTime SubmittedAtUtc { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewNote { get; set; }

    public Guid? PersonalTransactionId { get; set; }

    public Guid? HouseholdEntryId { get; set; }
}
