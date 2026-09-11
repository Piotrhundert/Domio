
namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdInvoice
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public string Supplier { get; set; } = string.Empty;

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime IssueDateUtc { get; set; }

    public DateTime DueDateUtc { get; set; }

    public long GrossAmountMinor { get; set; }

    public string StatusCode { get; set; } =
        HouseholdInvoiceStatuses.Unpaid;

    public string CategoryCode { get; set; } =
        HouseholdInvoiceCategories.Other;

    public DateTime? BillingPeriodFromUtc { get; set; }

    public DateTime? BillingPeriodToUtc { get; set; }

    public string? MainMeterNumber { get; set; }

    public string? MainMeterUnit { get; set; }

    public string? MainMeterPreviousReading { get; set; }

    public string? MainMeterCurrentReading { get; set; }

    public string? SubmeterReadingsSnapshot { get; set; }

    // Powiązanie z przyszłym modułem faktur mediów.
    // Na etapie M04 nie tworzymy FK do encji, która jeszcze nie istnieje.
    public Guid? UtilityInvoiceId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}
