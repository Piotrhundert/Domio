namespace Domio.Domain.Users;

public sealed class PersonProfile
{
    public Guid PersonId { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? Pesel { get; set; }

    public string? Nationality { get; set; }

    public string? IdentityDocumentTypeCode { get; set; }

    public string? IdentityDocumentNumber { get; set; }

    public string? IdentityDocumentIssuingCountry { get; set; }

    public DateTime? IdentityDocumentIssuedOn { get; set; }

    public DateTime? IdentityDocumentExpiresOn { get; set; }

    public string? PreferredContactMethodCode { get; set; }

    public string? CorrespondenceCountry { get; set; }

    public string? CorrespondenceRegion { get; set; }

    public string? CorrespondenceCity { get; set; }

    public string? CorrespondencePostalCode { get; set; }

    public string? CorrespondenceStreet { get; set; }

    public string? CorrespondenceBuildingNumber { get; set; }

    public string? CorrespondenceUnitNumber { get; set; }

    public string? CorrespondenceNotes { get; set; }

    public string? EmergencyContactFirstName { get; set; }

    public string? EmergencyContactLastName { get; set; }

    public string? EmergencyContactRelation { get; set; }

    public string? EmergencyContactPhone { get; set; }

    public string? EmergencyContactEmail { get; set; }

    public string? EmergencyContactNotes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
