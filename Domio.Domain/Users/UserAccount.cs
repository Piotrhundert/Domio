namespace Domio.Domain.Users;

public sealed class UserAccount
{
    public Guid Id { get; set; }

    public Guid PersonId { get; set; }

    public int RoleDefinitionId { get; set; }

    public string LoginName { get; set; } = string.Empty;

    public string NormalizedLoginName { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? PasswordChangedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }
}
