using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.Users;

public sealed class EditUserViewModel
{
    [Required]
    public Guid UserId { get; set; }

    public Guid PersonId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string LoginName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj adres e-mail.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    [MaxLength(254)]
    [Display(Name = "E-mail konta")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Konto aktywne")]
    public bool IsActive { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Wybierz rolę.")]
    [Display(Name = "Rola")]
    public int RoleDefinitionId { get; set; }

    public string RoleNamePl { get; set; } = string.Empty;

    public List<SelectListItem> Roles { get; set; } = [];
}
