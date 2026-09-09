using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.Roles;

public sealed class EditRoleViewModel
{
    public int RoleId { get; set; }

    public string Code { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public bool UsesDefaultPermissions { get; set; }

    [Required(ErrorMessage = "Podaj nazwę roli.")]
    [MaxLength(100)]
    [Display(Name = "Nazwa roli")]
    public string NamePl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj opis roli.")]
    [MaxLength(1000)]
    [Display(Name = "Opis roli")]
    public string DescriptionPl { get; set; } = string.Empty;

    public List<EditRolePermissionViewModel> Permissions { get; set; } = [];
}

public sealed class EditRolePermissionViewModel
{
    public string Code { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public string ModuleNamePl { get; set; } = string.Empty;

    public string NamePl { get; set; } = string.Empty;

    public string DescriptionPl { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = string.Empty;

    public bool IsAssigned { get; set; }

    public string ScopeCode { get; set; } = string.Empty;

    public bool IsProtected { get; set; }

    public List<SelectListItem> ScopeOptions { get; set; } = [];
}
