namespace Domio.Domain.PersonalFinance;

public sealed class PersonalFinancialTransaction
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Guid OwnerPersonId { get; set; }

    public string KindCode { get; set; } = string.Empty;

    // Wartość ze znakiem w najmniejszej jednostce waluty:
    // + przychód, - wydatek.
    public long AmountMinor { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? Description { get; set; }

    public Guid? CorrectsTransactionId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
