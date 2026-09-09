using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.Users;

public sealed class EditUserViewModel
{
    [Required]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Podaj imię.")]
    [MaxLength(100)]
    [Display(Name = "Imię")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwisko.")]
    [MaxLength(100)]
    [Display(Name = "Nazwisko")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Nazwa wyświetlana")]
    public string? DisplayName { get; set; }

    [MaxLength(50)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

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
