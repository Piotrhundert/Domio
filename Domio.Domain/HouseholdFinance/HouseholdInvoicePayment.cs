
namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdInvoicePayment
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid InvoiceId { get; set; }

    public Guid HouseholdAccountId { get; set; }

    // Id polecenia z formularza. Chroni przed podwójnym księgowaniem
    // po ponownym wysłaniu tego samego żądania.
    public Guid CommandId { get; set; }

    public long AmountMinor { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public Guid HouseholdEntryId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
