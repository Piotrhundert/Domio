using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Models.Profiles;

public sealed class EditProfileViewModel
{
    public Guid PersonId { get; set; }

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

    [Display(Name = "Typ osoby")]
    public string? PersonTypeCode { get; set; }

    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    [MaxLength(254)]
    [Display(Name = "E-mail kontaktowy")]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Notatki")]
    public string? Notes { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data urodzenia")]
    public DateTime? BirthDate { get; set; }

    [MaxLength(11)]
    [Display(Name = "PESEL")]
    public string? Pesel { get; set; }

    [MaxLength(100)]
    [Display(Name = "Narodowość")]
    public string? Nationality { get; set; }

    [Display(Name = "Typ dokumentu")]
    public string? IdentityDocumentTypeCode { get; set; }

    [MaxLength(100)]
    [Display(Name = "Numer dokumentu")]
    public string? IdentityDocumentNumber { get; set; }

    [MaxLength(100)]
    [Display(Name = "Kraj wydania")]
    public string? IdentityDocumentIssuingCountry { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data wydania")]
    public DateTime? IdentityDocumentIssuedOn { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Data ważności")]
    public DateTime? IdentityDocumentExpiresOn { get; set; }

    [Display(Name = "Preferowany sposób kontaktu")]
    public string? PreferredContactMethodCode { get; set; }

    [MaxLength(100)]
    [Display(Name = "Kraj")]
    public string? CorrespondenceCountry { get; set; }

    [MaxLength(100)]
    [Display(Name = "Województwo / region")]
    public string? CorrespondenceRegion { get; set; }

    [MaxLength(100)]
    [Display(Name = "Miejscowość")]
    public string? CorrespondenceCity { get; set; }

    [MaxLength(20)]
    [Display(Name = "Kod pocztowy")]
    public string? CorrespondencePostalCode { get; set; }

    [MaxLength(150)]
    [Display(Name = "Ulica")]
    public string? CorrespondenceStreet { get; set; }

    [MaxLength(30)]
    [Display(Name = "Numer budynku")]
    public string? CorrespondenceBuildingNumber { get; set; }

    [MaxLength(30)]
    [Display(Name = "Numer lokalu")]
    public string? CorrespondenceUnitNumber { get; set; }

    [MaxLength(500)]
    [Display(Name = "Uwagi do adresu")]
    public string? CorrespondenceNotes { get; set; }

    [MaxLength(100)]
    [Display(Name = "Imię")]
    public string? EmergencyContactFirstName { get; set; }

    [MaxLength(100)]
    [Display(Name = "Nazwisko")]
    public string? EmergencyContactLastName { get; set; }

    [MaxLength(100)]
    [Display(Name = "Relacja")]
    public string? EmergencyContactRelation { get; set; }

    [MaxLength(50)]
    [Display(Name = "Telefon")]
    public string? EmergencyContactPhone { get; set; }

    [EmailAddress(ErrorMessage = "Podaj poprawny adres e-mail.")]
    [MaxLength(254)]
    [Display(Name = "E-mail")]
    public string? EmergencyContactEmail { get; set; }

    [MaxLength(500)]
    [Display(Name = "Dodatkowe informacje")]
    public string? EmergencyContactNotes { get; set; }

    public List<SelectListItem> PersonTypes { get; set; } = [];

    public List<SelectListItem> IdentityDocumentTypes { get; set; } = [];

    public List<SelectListItem> PreferredContactMethods { get; set; } = [];
}
