namespace Domio.Domain.PersonalFinance;

public sealed class PersonalFinancialAccount
{
    public Guid Id { get; set; }

    public Guid OwnerPersonId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountTypeCode { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "PLN";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? ArchivedAtUtc { get; set; }
}
