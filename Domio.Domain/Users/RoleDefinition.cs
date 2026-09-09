namespace Domio.Domain.Users;

public sealed class RoleDefinition
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NamePl { get; set; } = string.Empty;

    public string DescriptionPl { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public string? PermissionConfigurationJson { get; set; }
}
