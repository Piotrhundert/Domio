namespace Domio.Domain.PersonalFinance;

public sealed class PersonalRecurringOccurrence
{
    public Guid Id { get; set; }

    public Guid RecurringRuleId { get; set; }

    public Guid OwnerPersonId { get; set; }

    // Snapshot of the plan at generation time.
    // Nullable only for safe migration from M03.3; migration backfills it.
    public Guid? AccountId { get; set; }

    public string? AccountName { get; set; }

    public string? KindCode { get; set; }

    public string? RuleName { get; set; }

    public string? CategoryCode { get; set; }

    public string? Counterparty { get; set; }

    public string PeriodKey { get; set; } = string.Empty;

    public DateTime PlannedDateUtc { get; set; }

    public long PlannedAmountMinor { get; set; }

    public string CurrencyCode { get; set; } = "PLN";

    public string StatusCode { get; set; } =
        PersonalRecurringOccurrenceStatuses.Planned;

    public Guid? ActualTransactionId { get; set; }

    public long? ActualAmountMinor { get; set; }

    public DateTime? ActualDateUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
