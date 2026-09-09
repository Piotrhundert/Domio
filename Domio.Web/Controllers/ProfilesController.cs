using System.Security.Claims;
using Domio.Application.Authorization;
using Domio.Application.Profiles;
using Domio.Domain.Users;
using Domio.Web.Models.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class ProfilesController(
    IProfileService profileService) : Controller
{
    [HttpGet]
    public IActionResult My()
    {
        var personId =
            GetCurrentPersonId();

        return RedirectToAction(
            nameof(Details),
            new { id = personId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profile =
                await profileService.GetAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

            if (profile is null)
            {
                return NotFound();
            }

            return View(profile);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profile =
                await profileService.GetAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

            if (profile is null)
            {
                return NotFound();
            }

            if (!profile.CanEdit)
            {
                return Forbid();
            }

            return View(BuildModel(profile));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EditProfileViewModel model,
        CancellationToken cancellationToken = default)
    {
        RebuildOptions(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await profileService.UpdateAsync(
                new UpdatePersonProfileRequest(
                    model.PersonId,
                    model.FirstName,
                    model.LastName,
                    model.DisplayName,
                    model.PersonTypeCode,
                    model.ContactEmail,
                    model.Phone,
                    model.Notes,
                    model.BirthDate,
                    model.Pesel,
                    model.Nationality,
                    model.IdentityDocumentTypeCode,
                    model.IdentityDocumentNumber,
                    model.IdentityDocumentIssuingCountry,
                    model.IdentityDocumentIssuedOn,
                    model.IdentityDocumentExpiresOn,
                    model.PreferredContactMethodCode,
                    model.CorrespondenceCountry,
                    model.CorrespondenceRegion,
                    model.CorrespondenceCity,
                    model.CorrespondencePostalCode,
                    model.CorrespondenceStreet,
                    model.CorrespondenceBuildingNumber,
                    model.CorrespondenceUnitNumber,
                    model.CorrespondenceNotes,
                    model.EmergencyContactFirstName,
                    model.EmergencyContactLastName,
                    model.EmergencyContactRelation,
                    model.EmergencyContactPhone,
                    model.EmergencyContactEmail,
                    model.EmergencyContactNotes),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["ProfileMessage"] =
            "Profil został zapisany.";

        return RedirectToAction(
            nameof(Details),
            new { id = model.PersonId });
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException(
                "Nie można ustalić identyfikatora zalogowanego użytkownika.");
        }

        return userId;
    }

    private Guid GetCurrentPersonId()
    {
        var value =
            User.FindFirstValue(
                DomioClaimTypes.PersonId);

        if (!Guid.TryParse(value, out var personId))
        {
            throw new InvalidOperationException(
                "Nie można ustalić profilu zalogowanego użytkownika.");
        }

        return personId;
    }

    private static EditProfileViewModel BuildModel(
        PersonProfileData profile)
    {
        var model = new EditProfileViewModel
        {
            PersonId = profile.PersonId,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            DisplayName = profile.DisplayName,
            PersonTypeCode = profile.PersonTypeCode,
            ContactEmail = profile.ContactEmail,
            Phone = profile.Phone,
            Notes = profile.Notes,
            BirthDate = profile.BirthDate,
            Pesel = profile.Pesel,
            Nationality = profile.Nationality,
            IdentityDocumentTypeCode =
                profile.IdentityDocumentTypeCode,
            IdentityDocumentNumber =
                profile.IdentityDocumentNumber,
            IdentityDocumentIssuingCountry =
                profile.IdentityDocumentIssuingCountry,
            IdentityDocumentIssuedOn =
                profile.IdentityDocumentIssuedOn,
            IdentityDocumentExpiresOn =
                profile.IdentityDocumentExpiresOn,
            PreferredContactMethodCode =
                profile.PreferredContactMethodCode,
            CorrespondenceCountry =
                profile.CorrespondenceCountry,
            CorrespondenceRegion =
                profile.CorrespondenceRegion,
            CorrespondenceCity =
                profile.CorrespondenceCity,
            CorrespondencePostalCode =
                profile.CorrespondencePostalCode,
            CorrespondenceStreet =
                profile.CorrespondenceStreet,
            CorrespondenceBuildingNumber =
                profile.CorrespondenceBuildingNumber,
            CorrespondenceUnitNumber =
                profile.CorrespondenceUnitNumber,
            CorrespondenceNotes =
                profile.CorrespondenceNotes,
            EmergencyContactFirstName =
                profile.EmergencyContactFirstName,
            EmergencyContactLastName =
                profile.EmergencyContactLastName,
            EmergencyContactRelation =
                profile.EmergencyContactRelation,
            EmergencyContactPhone =
                profile.EmergencyContactPhone,
            EmergencyContactEmail =
                profile.EmergencyContactEmail,
            EmergencyContactNotes =
                profile.EmergencyContactNotes
        };

        RebuildOptions(model);

        return model;
    }

    private static void RebuildOptions(
        EditProfileViewModel model)
    {
        model.PersonTypes =
            BuildOptions(
                Domio.Domain.Users.PersonTypes.All,
                model.PersonTypeCode,
                "Nie określono");

        model.IdentityDocumentTypes =
            BuildOptions(
                Domio.Domain.Users.IdentityDocumentTypes.All,
                model.IdentityDocumentTypeCode,
                "Nie określono");

        model.PreferredContactMethods =
            BuildOptions(
                Domio.Domain.Users.PreferredContactMethods.All,
                model.PreferredContactMethodCode,
                "Nie określono");
    }

    private static List<SelectListItem> BuildOptions(
        IReadOnlyList<ProfileDictionaryItem> items,
        string? selected,
        string emptyText)
    {
        var result =
            new List<SelectListItem>
            {
                new()
                {
                    Value = string.Empty,
                    Text = emptyText,
                    Selected =
                        string.IsNullOrWhiteSpace(
                            selected)
                }
            };

        result.AddRange(
            items.Select(x =>
                new SelectListItem
                {
                    Value = x.Code,
                    Text = x.NamePl,
                    Selected =
                        string.Equals(
                            x.Code,
                            selected,
                            StringComparison.Ordinal)
                }));

        return result;
    }
}
