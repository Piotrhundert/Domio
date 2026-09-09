using System.ComponentModel.DataAnnotations;

namespace Domio.Web.Models.Users;

public sealed class CreatePersonViewModel
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

    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    [MaxLength(254)]
    [Display(Name = "E-mail kontaktowy")]
    public string? Email { get; set; }

    [MaxLength(50)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }
}
