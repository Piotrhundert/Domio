using System.Net.Mail;
using System.Text.Json;
using Domio.Application.Auditing;
using Domio.Application.Profiles;
using Domio.Domain.Users;
using Domio.Infrastructure.Authorization;
using Domio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domio.Infrastructure.Profiles;

public sealed class ProfileService(
    DomioDbContext dbContext,
    IAuditService auditService) : IProfileService
{
    public async Task<PersonProfileData?> GetAsync(
        Guid personId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var isOwn =
            actorPersonId == personId;

        var canView =
            isOwn
                ? await HasAnyPermissionAsync(
                    actorUserId,
                    [
                        SystemPermissions.ProfileViewOwn,
                        SystemPermissions.ProfileViewAll
                    ],
                    cancellationToken)
                : await PermissionEnforcement.HasUserAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.ProfileViewAll,
                    cancellationToken);

        if (!canView)
        {
            throw new UnauthorizedAccessException(
                "Brak uprawnienia do wyświetlenia tego profilu.");
        }

        var canEdit =
            isOwn
                ? await HasAnyPermissionAsync(
                    actorUserId,
                    [
                        SystemPermissions.ProfileEditOwn,
                        SystemPermissions.ProfileEditAll
                    ],
                    cancellationToken)
                : await PermissionEnforcement.HasUserAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.ProfileEditAll,
                    cancellationToken);

        var person =
            await dbContext.People
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == personId,
                    cancellationToken);

        if (person is null)
        {
            return null;
        }

        var profile =
            await dbContext.PersonProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.PersonId == personId,
                    cancellationToken);

        var account =
            await (
                from userAccount in dbContext.UserAccounts.AsNoTracking()
                join role in dbContext.RoleDefinitions.AsNoTracking()
                    on userAccount.RoleDefinitionId equals role.Id
                where userAccount.PersonId == personId
                select new
                {
                    Account = userAccount,
                    Role = role
                })
                .SingleOrDefaultAsync(cancellationToken);

        var displayName =
            string.IsNullOrWhiteSpace(person.DisplayName)
                ? $"{person.FirstName} {person.LastName}".Trim()
                : person.DisplayName!.Trim();

        var birthDate =
            profile?.BirthDate;

        return new PersonProfileData(
            person.Id,
            account?.Account.Id,
            person.FirstName,
            person.LastName,
            displayName,
            person.PersonTypeCode,
            PersonTypes.GetNamePl(person.PersonTypeCode),
            person.Email,
            person.Phone,
            person.Notes,
            person.IsActive,
            person.CreatedAtUtc,
            person.UpdatedAtUtc,
            person.ArchivedAtUtc,
            birthDate,
            CalculateAge(birthDate),
            profile?.Pesel,
            profile?.Nationality,
            profile?.IdentityDocumentTypeCode,
            IdentityDocumentTypes.GetNamePl(
                profile?.IdentityDocumentTypeCode),
            profile?.IdentityDocumentNumber,
            profile?.IdentityDocumentIssuingCountry,
            profile?.IdentityDocumentIssuedOn,
            profile?.IdentityDocumentExpiresOn,
            profile?.PreferredContactMethodCode,
            PreferredContactMethods.GetNamePl(
                profile?.PreferredContactMethodCode),
            profile?.CorrespondenceCountry,
            profile?.CorrespondenceRegion,
            profile?.CorrespondenceCity,
            profile?.CorrespondencePostalCode,
            profile?.CorrespondenceStreet,
            profile?.CorrespondenceBuildingNumber,
            profile?.CorrespondenceUnitNumber,
            profile?.CorrespondenceNotes,
            profile?.EmergencyContactFirstName,
            profile?.EmergencyContactLastName,
            profile?.EmergencyContactRelation,
            profile?.EmergencyContactPhone,
            profile?.EmergencyContactEmail,
            profile?.EmergencyContactNotes,
            account?.Account.LoginName,
            account?.Account.Email,
            account?.Account.IsActive,
            account?.Role.Code,
            account?.Role.NamePl,
            account?.Account.LastLoginAtUtc,
            account?.Account.PasswordChangedAtUtc,
            account?.Account.LockoutEndUtc,
            canEdit);
    }

    public async Task UpdateAsync(
        UpdatePersonProfileRequest request,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actorPersonId =
            await GetActorPersonIdAsync(
                actorUserId,
                cancellationToken);

        var isOwn =
            actorPersonId == request.PersonId;

        var canEdit =
            isOwn
                ? await HasAnyPermissionAsync(
                    actorUserId,
                    [
                        SystemPermissions.ProfileEditOwn,
                        SystemPermissions.ProfileEditAll
                    ],
                    cancellationToken)
                : await PermissionEnforcement.HasUserAsync(
                    dbContext,
                    actorUserId,
                    SystemPermissions.ProfileEditAll,
                    cancellationToken);

        if (!canEdit)
        {
            throw new UnauthorizedAccessException(
                "Brak uprawnienia do edycji tego profilu.");
        }

        Validate(request);

        var person = await dbContext.People
            .SingleOrDefaultAsync(
                x => x.Id == request.PersonId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Osoba nie istnieje.");

        var profile = await dbContext.PersonProfiles
            .SingleOrDefaultAsync(
                x => x.PersonId == request.PersonId,
                cancellationToken);

        var normalizedPesel =
            NormalizeOptional(request.Pesel);

        if (normalizedPesel is not null &&
            await dbContext.PersonProfiles
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.PersonId != request.PersonId &&
                        x.Pesel == normalizedPesel,
                    cancellationToken))
        {
            throw new InvalidOperationException(
                "Podany PESEL jest już przypisany do innej osoby.");
        }

        var now = DateTime.UtcNow;

        if (profile is null)
        {
            profile = new PersonProfile
            {
                PersonId = person.Id,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.PersonProfiles.Add(profile);
        }

        var changedFields =
            GetChangedFields(
                person,
                profile,
                request);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        person.FirstName = request.FirstName.Trim();
        person.LastName = request.LastName.Trim();
        person.DisplayName =
            NormalizeOptional(request.DisplayName);
        person.PersonTypeCode =
            NormalizeOptional(request.PersonTypeCode);
        person.Email =
            NormalizeOptional(request.ContactEmail);
        person.Phone =
            NormalizeOptional(request.Phone);
        person.Notes =
            NormalizeOptional(request.Notes);
        person.UpdatedAtUtc = now;

        profile.BirthDate =
            NormalizeDate(request.BirthDate);
        profile.Pesel =
            NormalizeOptional(request.Pesel);
        profile.Nationality =
            NormalizeOptional(request.Nationality);
        profile.IdentityDocumentTypeCode =
            NormalizeOptional(
                request.IdentityDocumentTypeCode);
        profile.IdentityDocumentNumber =
            NormalizeOptional(
                request.IdentityDocumentNumber);
        profile.IdentityDocumentIssuingCountry =
            NormalizeOptional(
                request.IdentityDocumentIssuingCountry);
        profile.IdentityDocumentIssuedOn =
            NormalizeDate(
                request.IdentityDocumentIssuedOn);
        profile.IdentityDocumentExpiresOn =
            NormalizeDate(
                request.IdentityDocumentExpiresOn);
        profile.PreferredContactMethodCode =
            NormalizeOptional(
                request.PreferredContactMethodCode);
        profile.CorrespondenceCountry =
            NormalizeOptional(
                request.CorrespondenceCountry);
        profile.CorrespondenceRegion =
            NormalizeOptional(
                request.CorrespondenceRegion);
        profile.CorrespondenceCity =
            NormalizeOptional(
                request.CorrespondenceCity);
        profile.CorrespondencePostalCode =
            NormalizeOptional(
                request.CorrespondencePostalCode);
        profile.CorrespondenceStreet =
            NormalizeOptional(
                request.CorrespondenceStreet);
        profile.CorrespondenceBuildingNumber =
            NormalizeOptional(
                request.CorrespondenceBuildingNumber);
        profile.CorrespondenceUnitNumber =
            NormalizeOptional(
                request.CorrespondenceUnitNumber);
        profile.CorrespondenceNotes =
            NormalizeOptional(
                request.CorrespondenceNotes);
        profile.EmergencyContactFirstName =
            NormalizeOptional(
                request.EmergencyContactFirstName);
        profile.EmergencyContactLastName =
            NormalizeOptional(
                request.EmergencyContactLastName);
        profile.EmergencyContactRelation =
            NormalizeOptional(
                request.EmergencyContactRelation);
        profile.EmergencyContactPhone =
            NormalizeOptional(
                request.EmergencyContactPhone);
        profile.EmergencyContactEmail =
            NormalizeOptional(
                request.EmergencyContactEmail);
        profile.EmergencyContactNotes =
            NormalizeOptional(
                request.EmergencyContactNotes);
        profile.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        await auditService.WriteAsync(
            new AuditEntry(
                EventType: "M02.7.ProfileUpdated",
                EntityType: "Person",
                EntityId: person.Id.ToString(),
                ActorId: actorUserId.ToString(),
                CorrelationId: correlationId,
                Description:
                    "Zaktualizowano profil osoby. W audycie zapisano tylko nazwy zmienionych pól; wartości PESEL i dokumentu tożsamości nie są rejestrowane.",
                NewValuesJson: JsonSerializer.Serialize(new
                {
                    ChangedFields = changedFields
                })),
            cancellationToken);

        await transaction.CommitAsync(
            cancellationToken);
    }

    private async Task<Guid> GetActorPersonIdAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var personId = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(x =>
                x.Id == actorUserId &&
                x.IsActive)
            .Select(x => (Guid?)x.PersonId)
            .SingleOrDefaultAsync(cancellationToken);

        return personId
            ?? throw new UnauthorizedAccessException(
                "Nie można ustalić profilu zalogowanego użytkownika.");
    }

    private async Task<bool> HasAnyPermissionAsync(
        Guid actorUserId,
        IReadOnlyList<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        foreach (var permissionCode in permissionCodes)
        {
            if (await PermissionEnforcement.HasUserAsync(
                    dbContext,
                    actorUserId,
                    permissionCode,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static void Validate(
        UpdatePersonProfileRequest request)
    {
        ValidateRequiredText(request.FirstName, "Imię", 100);
        ValidateRequiredText(request.LastName, "Nazwisko", 100);

        ValidateOptionalText(request.DisplayName, "Nazwa wyświetlana", 200);
        ValidateOptionalText(request.ContactEmail, "E-mail kontaktowy", 254);
        ValidateOptionalText(request.Phone, "Telefon", 50);
        ValidateOptionalText(request.Notes, "Notatki", 2000);
        ValidateOptionalText(request.Pesel, "PESEL", 11);
        ValidateOptionalText(request.Nationality, "Narodowość", 100);
        ValidateOptionalText(request.IdentityDocumentNumber, "Numer dokumentu", 100);
        ValidateOptionalText(request.IdentityDocumentIssuingCountry, "Kraj wydania dokumentu", 100);
        ValidateOptionalText(request.CorrespondenceCountry, "Kraj", 100);
        ValidateOptionalText(request.CorrespondenceRegion, "Województwo / region", 100);
        ValidateOptionalText(request.CorrespondenceCity, "Miejscowość", 100);
        ValidateOptionalText(request.CorrespondencePostalCode, "Kod pocztowy", 20);
        ValidateOptionalText(request.CorrespondenceStreet, "Ulica", 150);
        ValidateOptionalText(request.CorrespondenceBuildingNumber, "Numer budynku", 30);
        ValidateOptionalText(request.CorrespondenceUnitNumber, "Numer lokalu", 30);
        ValidateOptionalText(request.CorrespondenceNotes, "Uwagi do adresu", 500);
        ValidateOptionalText(request.EmergencyContactFirstName, "Imię osoby kontaktowej", 100);
        ValidateOptionalText(request.EmergencyContactLastName, "Nazwisko osoby kontaktowej", 100);
        ValidateOptionalText(request.EmergencyContactRelation, "Relacja", 100);
        ValidateOptionalText(request.EmergencyContactPhone, "Telefon osoby kontaktowej", 50);
        ValidateOptionalText(request.EmergencyContactEmail, "E-mail osoby kontaktowej", 254);
        ValidateOptionalText(request.EmergencyContactNotes, "Uwagi o osobie kontaktowej", 500);

        if (!PersonTypes.IsValid(request.PersonTypeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy typ osoby.");
        }

        if (!IdentityDocumentTypes.IsValid(
                request.IdentityDocumentTypeCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy typ dokumentu.");
        }

        if (!PreferredContactMethods.IsValid(
                request.PreferredContactMethodCode))
        {
            throw new ArgumentException(
                "Wybrano nieprawidłowy preferowany sposób kontaktu.");
        }

        ValidateEmail(request.ContactEmail, "E-mail kontaktowy");
        ValidateEmail(request.EmergencyContactEmail, "E-mail osoby kontaktowej");

        if (request.BirthDate.HasValue &&
            request.BirthDate.Value.Date > DateTime.Today)
        {
            throw new ArgumentException(
                "Data urodzenia nie może być datą przyszłą.");
        }

        if (request.IdentityDocumentIssuedOn.HasValue &&
            request.IdentityDocumentExpiresOn.HasValue &&
            request.IdentityDocumentExpiresOn.Value.Date <
                request.IdentityDocumentIssuedOn.Value.Date)
        {
            throw new ArgumentException(
                "Data ważności dokumentu nie może być wcześniejsza od daty wydania.");
        }

        var pesel =
            NormalizeOptional(request.Pesel);

        if (pesel is not null &&
            !IsValidPesel(pesel))
        {
            throw new ArgumentException(
                "PESEL musi zawierać 11 cyfr i mieć poprawną cyfrę kontrolną.");
        }
    }

    private static bool IsValidPesel(string pesel)
    {
        if (pesel.Length != 11 ||
            pesel.Any(x => !char.IsDigit(x)))
        {
            return false;
        }

        int[] weights =
            [1, 3, 7, 9, 1, 3, 7, 9, 1, 3];

        var sum = 0;

        for (var i = 0; i < 10; i++)
        {
            sum +=
                (pesel[i] - '0') *
                weights[i];
        }

        var control =
            (10 - (sum % 10)) % 10;

        return control ==
            pesel[10] - '0';
    }

    private static int? CalculateAge(DateTime? birthDate)
    {
        if (!birthDate.HasValue)
        {
            return null;
        }

        var today = DateTime.Today;
        var date = birthDate.Value.Date;
        var age = today.Year - date.Year;

        if (date > today.AddYears(-age).Date)
        {
            age--;
        }

        return Math.Max(0, age);
    }

    private static IReadOnlyList<string> GetChangedFields(
        Person person,
        PersonProfile profile,
        UpdatePersonProfileRequest request)
    {
        var fields = new List<string>();

        AddIfDifferent(fields, "FirstName", person.FirstName, request.FirstName.Trim());
        AddIfDifferent(fields, "LastName", person.LastName, request.LastName.Trim());
        AddIfDifferent(fields, "DisplayName", NormalizeOptional(person.DisplayName), NormalizeOptional(request.DisplayName));
        AddIfDifferent(fields, "PersonTypeCode", NormalizeOptional(person.PersonTypeCode), NormalizeOptional(request.PersonTypeCode));
        AddIfDifferent(fields, "ContactEmail", NormalizeOptional(person.Email), NormalizeOptional(request.ContactEmail));
        AddIfDifferent(fields, "Phone", NormalizeOptional(person.Phone), NormalizeOptional(request.Phone));
        AddIfDifferent(fields, "Notes", NormalizeOptional(person.Notes), NormalizeOptional(request.Notes));
        AddIfDifferent(fields, "BirthDate", NormalizeDate(profile.BirthDate), NormalizeDate(request.BirthDate));
        AddIfDifferent(fields, "Pesel", NormalizeOptional(profile.Pesel), NormalizeOptional(request.Pesel));
        AddIfDifferent(fields, "Nationality", NormalizeOptional(profile.Nationality), NormalizeOptional(request.Nationality));
        AddIfDifferent(fields, "IdentityDocumentTypeCode", NormalizeOptional(profile.IdentityDocumentTypeCode), NormalizeOptional(request.IdentityDocumentTypeCode));
        AddIfDifferent(fields, "IdentityDocumentNumber", NormalizeOptional(profile.IdentityDocumentNumber), NormalizeOptional(request.IdentityDocumentNumber));
        AddIfDifferent(fields, "IdentityDocumentIssuingCountry", NormalizeOptional(profile.IdentityDocumentIssuingCountry), NormalizeOptional(request.IdentityDocumentIssuingCountry));
        AddIfDifferent(fields, "IdentityDocumentIssuedOn", NormalizeDate(profile.IdentityDocumentIssuedOn), NormalizeDate(request.IdentityDocumentIssuedOn));
        AddIfDifferent(fields, "IdentityDocumentExpiresOn", NormalizeDate(profile.IdentityDocumentExpiresOn), NormalizeDate(request.IdentityDocumentExpiresOn));
        AddIfDifferent(fields, "PreferredContactMethodCode", NormalizeOptional(profile.PreferredContactMethodCode), NormalizeOptional(request.PreferredContactMethodCode));
        AddIfDifferent(fields, "CorrespondenceAddress", BuildAddressFingerprint(profile), BuildAddressFingerprint(request));
        AddIfDifferent(fields, "EmergencyContact", BuildEmergencyFingerprint(profile), BuildEmergencyFingerprint(request));

        return fields;
    }

    private static string BuildAddressFingerprint(PersonProfile profile) =>
        string.Join("|",
            profile.CorrespondenceCountry,
            profile.CorrespondenceRegion,
            profile.CorrespondenceCity,
            profile.CorrespondencePostalCode,
            profile.CorrespondenceStreet,
            profile.CorrespondenceBuildingNumber,
            profile.CorrespondenceUnitNumber,
            profile.CorrespondenceNotes);

    private static string BuildAddressFingerprint(UpdatePersonProfileRequest request) =>
        string.Join("|",
            request.CorrespondenceCountry,
            request.CorrespondenceRegion,
            request.CorrespondenceCity,
            request.CorrespondencePostalCode,
            request.CorrespondenceStreet,
            request.CorrespondenceBuildingNumber,
            request.CorrespondenceUnitNumber,
            request.CorrespondenceNotes);

    private static string BuildEmergencyFingerprint(PersonProfile profile) =>
        string.Join("|",
            profile.EmergencyContactFirstName,
            profile.EmergencyContactLastName,
            profile.EmergencyContactRelation,
            profile.EmergencyContactPhone,
            profile.EmergencyContactEmail,
            profile.EmergencyContactNotes);

    private static string BuildEmergencyFingerprint(UpdatePersonProfileRequest request) =>
        string.Join("|",
            request.EmergencyContactFirstName,
            request.EmergencyContactLastName,
            request.EmergencyContactRelation,
            request.EmergencyContactPhone,
            request.EmergencyContactEmail,
            request.EmergencyContactNotes);

    private static void AddIfDifferent<T>(
        ICollection<string> fields,
        string fieldName,
        T oldValue,
        T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(
                oldValue,
                newValue))
        {
            fields.Add(fieldName);
        }
    }

    private static void ValidateRequiredText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{fieldName} nie może być puste.");
        }

        if (value.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }
    }

    private static void ValidateOptionalText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            value.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"{fieldName} może mieć maksymalnie {maxLength} znaków.");
        }
    }

    private static void ValidateEmail(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            var parsed =
                new MailAddress(value.Trim());

            if (!string.Equals(
                    parsed.Address,
                    value.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                $"{fieldName}: podaj poprawny adres e-mail.");
        }
    }

    private static DateTime? NormalizeDate(DateTime? value) =>
        value?.Date;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
