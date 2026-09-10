namespace Domio.Domain.HouseholdFinance;

public sealed class Household
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    public bool IsActive { get; set; } = true;

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
