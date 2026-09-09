using System.Security.Claims;
using Domio.Application.Users;
using Domio.Web.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class UsersController(
    IUserDirectoryService userDirectoryService,
    IUserManagementService userManagementService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await userDirectoryService.GetOverviewAsync(
                cancellationToken);

        return View(overview);
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        CancellationToken cancellationToken = default)
    {
        var options =
            await userManagementService.GetCreateOptionsAsync(
                cancellationToken);

        return View(
            new CreateUserViewModel
            {
                Roles = BuildRoleItems(options.Roles)
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateUserViewModel model,
        CancellationToken cancellationToken = default)
    {
        ValidatePasswordComplexity(
            model.Password,
            nameof(model.Password));

        var options =
            await userManagementService.GetCreateOptionsAsync(
                cancellationToken);

        model.Roles = BuildRoleItems(
            options.Roles,
            model.RoleDefinitionId);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await userManagementService.CreateUserAsync(
                new CreateUserRequest(
                    ExistingPersonId: null,
                    model.FirstName,
                    model.LastName,
                    model.DisplayName,
                    model.Phone,
                    model.Email,
                    model.Password,
                    model.RoleDefinitionId),
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

        TempData["UsersMessage"] =
            "Użytkownik został utworzony. Login został nadany automatycznie.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult CreatePerson() =>
        View(new CreatePersonViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePerson(
        CreatePersonViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await userManagementService.CreatePersonAsync(
                new CreatePersonRequest(
                    model.FirstName,
                    model.LastName,
                    model.DisplayName,
                    model.Email,
                    model.Phone),
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

        TempData["UsersMessage"] =
            "Osoba została utworzona bez konta logowania.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreateForPerson(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var options =
            await userManagementService.GetCreateOptionsAsync(
                cancellationToken);

        var person = options.PeopleWithoutAccount
            .SingleOrDefault(x => x.PersonId == id);

        if (person is null)
        {
            return NotFound();
        }

        return View(
            new CreateAccountForPersonViewModel
            {
                PersonId = person.PersonId,
                PersonName = person.DisplayName,
                Email = person.Email ?? string.Empty,
                Roles = BuildRoleItems(options.Roles)
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateForPerson(
        CreateAccountForPersonViewModel model,
        CancellationToken cancellationToken = default)
    {
        ValidatePasswordComplexity(
            model.Password,
            nameof(model.Password));

        var options =
            await userManagementService.GetCreateOptionsAsync(
                cancellationToken);

        var person = options.PeopleWithoutAccount
            .SingleOrDefault(
                x => x.PersonId == model.PersonId);

        if (person is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Osoba nie istnieje albo ma już konto.");

            model.PersonName =
                string.IsNullOrWhiteSpace(model.PersonName)
                    ? "Nieznana osoba"
                    : model.PersonName;
        }
        else
        {
            model.PersonName = person.DisplayName;
        }

        model.Roles = BuildRoleItems(
            options.Roles,
            model.RoleDefinitionId);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await userManagementService.CreateUserAsync(
                new CreateUserRequest(
                    model.PersonId,
                    FirstName: null,
                    LastName: null,
                    DisplayName: null,
                    Phone: null,
                    model.Email,
                    model.Password,
                    model.RoleDefinitionId),
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

        TempData["UsersMessage"] =
            "Konto zostało utworzone. Login został nadany automatycznie.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var data =
            await userManagementService.GetEditDataAsync(
                id,
                cancellationToken);

        if (data is null)
        {
            return NotFound();
        }

        return View(
            new EditUserViewModel
            {
                UserId = data.UserId,
                FirstName = data.FirstName,
                LastName = data.LastName,
                DisplayName = data.DisplayName,
                Phone = data.Phone,
                LoginName = data.LoginName,
                Email = data.Email ?? string.Empty,
                IsActive = data.IsActive,
                RoleNamePl = data.RoleNamePl
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EditUserViewModel model,
        CancellationToken cancellationToken = default)
    {
        var current =
            await userManagementService.GetEditDataAsync(
                model.UserId,
                cancellationToken);

        if (current is null)
        {
            return NotFound();
        }

        model.RoleNamePl = current.RoleNamePl;
        model.LoginName = current.LoginName;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await userManagementService.UpdateUserAsync(
                new UpdateUserRequest(
                    model.UserId,
                    model.FirstName,
                    model.LastName,
                    model.DisplayName,
                    model.Phone,
                    model.Email,
                    model.IsActive),
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

        TempData["UsersMessage"] =
            "Dane użytkownika zostały zapisane.";

        return RedirectToAction(nameof(Index));
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

    private static List<SelectListItem> BuildRoleItems(
        IReadOnlyList<UserRoleOption> roles,
        int? selectedId = null) =>
        roles.Select(role =>
            new SelectListItem
            {
                Value = role.Id.ToString(),
                Text = role.NamePl,
                Selected =
                    selectedId.HasValue &&
                    role.Id == selectedId.Value
            })
        .ToList();

    private void ValidatePasswordComplexity(
        string password,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit))
        {
            ModelState.AddModelError(
                fieldName,
                "Hasło musi zawierać małą literę, wielką literę i cyfrę.");
        }
    }
}
