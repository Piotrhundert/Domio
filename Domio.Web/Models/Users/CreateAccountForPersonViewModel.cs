using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.Users;

public sealed class CreateAccountForPersonViewModel
{
    [Required]
    public Guid PersonId { get; set; }

    public string PersonName { get; set; } = string.Empty;

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
