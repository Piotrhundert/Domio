namespace Domio.Domain.PersonalFinance;

public sealed class PersonalRecurringRule
{
    public Guid Id { get; set; }
    public Guid OwnerPersonId { get; set; }
    public Guid AccountId { get; set; }
    public string KindCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long PlannedAmountMinor { get; set; }
    public string CurrencyCode { get; set; } = "PLN";
    public string FrequencyCode { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? Counterparty { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
