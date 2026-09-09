using System.ComponentModel.DataAnnotations;

namespace Domio.Web.Models.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Podaj login.")]
    [Display(Name = "Login")]
    public string LoginName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Podaj hasło.")]
    [DataType(DataType.Password)]
    [Display(Name = "Hasło")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Zapamiętaj mnie")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
