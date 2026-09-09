using System.ComponentModel.DataAnnotations;

namespace Domio.Web.Models.Account;

public sealed class InitializeAdministratorViewModel
{
    [Required(ErrorMessage = "Podaj imię.")]
    [MaxLength(100)]
    [Display(Name = "Imię")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj nazwisko.")]
    [MaxLength(100)]
    [Display(Name = "Nazwisko")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj login.")]
    [MinLength(3, ErrorMessage = "Login musi mieć co najmniej 3 znaki.")]
    [MaxLength(100)]
    [Display(Name = "Login administratora")]
    public string LoginName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj hasło.")]
    [MinLength(
        10,
        ErrorMessage = "Hasło musi mieć co najmniej 10 znaków.")]
    [DataType(DataType.Password)]
    [Display(Name = "Hasło")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Powtórz hasło.")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(Password),
        ErrorMessage = "Hasła nie są takie same.")]
    [Display(Name = "Powtórz hasło")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
