namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdEntry
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid AccountId { get; set; }

    public string EntryTypeCode { get; set; } = string.Empty;

    // Wartość ze znakiem w najmniejszej jednostce waluty.
    public long AmountMinor { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string? CategoryCode { get; set; }

    public string? Description { get; set; }

    // Każde księgowanie może wskazać proces źródłowy.
    // W M04.1 dla operacji ręcznej SourceType = "Manual".
    public string? SourceType { get; set; }

    public string? SourceId { get; set; }

    public Guid? CorrectsEntryId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
