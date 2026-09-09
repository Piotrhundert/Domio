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
}
