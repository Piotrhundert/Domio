namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdContributionObligation
{
    public Guid Id { get; set; }

    public Guid ContributionRuleId { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid HouseholdMemberId { get; set; }

    public Guid TargetHouseholdAccountId { get; set; }

    public string PeriodKey { get; set; } = string.Empty;

    // Snapshot kwoty zobowiązania dla konkretnego miesiąca.
    public long AmountMinor { get; set; }

    public long PaidAmountMinor { get; set; }

    // Snapshot terminu dla konkretnego miesiąca.
    public DateTime DueDateUtc { get; set; }

    public string StatusCode { get; set; } =
        HouseholdContributionStatuses.Pending;

    public string ModeCode { get; set; } = string.Empty;

    public long? FixedAmountMinorSnapshot { get; set; }

    public int? PercentageBasisPointsSnapshot { get; set; }

    public long? PlannedIncomeAmountMinor { get; set; }

    public Guid? IncomeRuleId { get; set; }

    public Guid? IncomeOccurrenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
