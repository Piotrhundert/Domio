namespace Domio.Domain.HouseholdFinance;

public sealed class HouseholdMember
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid PersonId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAtUtc { get; set; }

    public DateTime? LeftAtUtc { get; set; }
}
