namespace Domio.Domain.Users;

public sealed class UserAccount
{
    public Guid Id { get; set; }

    public Guid PersonId { get; set; }

    public int RoleDefinitionId { get; set; }

    public string LoginName { get; set; } = string.Empty;

    public string NormalizedLoginName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
}
