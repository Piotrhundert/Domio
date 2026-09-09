using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.Users;

public sealed class CreateUserViewModel
{
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

    [Required(ErrorMessage = "Podaj adres e-mail.")]
    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    [MaxLength(254)]
    [Display(Name = "E-mail konta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj hasło.")]
    [MinLength(10, ErrorMessage = "Hasło musi mieć co najmniej 10 znaków.")]
    [DataType(DataType.Password)]
    [Display(Name = "Hasło początkowe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Powtórz hasło.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Hasła nie są takie same.")]
    [Display(Name = "Powtórz hasło")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Wybierz rolę.")]
    [Display(Name = "Rola")]
    public int RoleDefinitionId { get; set; }

    public List<SelectListItem> Roles { get; set; } = [];
}
