namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdContributionRule
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid HouseholdMemberId { get; set; }

    public string ModeCode { get; set; } = string.Empty;

    public long? FixedAmountMinor { get; set; }

    // 100 basis points = 1.00%; 3000 = 30.00%.
    public int? PercentageBasisPoints { get; set; }

    public Guid? IncomeRuleId { get; set; }

    public int DueOffsetDays { get; set; } = 7;

    public Guid TargetHouseholdAccountId { get; set; }

    public DateTime ValidFromUtc { get; set; }

    public DateTime? ValidToUtc { get; set; }

    public int ReminderDays { get; set; } = 3;

    public bool IsActive { get; set; } = true;

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
