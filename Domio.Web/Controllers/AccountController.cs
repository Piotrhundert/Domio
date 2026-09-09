using System.Security.Claims;
using Domio.Application.Authentication;
using Domio.Web.Models.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domio.Web.Controllers;

public sealed class AccountController(
    IAccountAuthenticationService authenticationService,
    IWebHostEnvironment environment) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(
                "Index",
                "Home");
        }

        var hasUsers =
            await authenticationService.HasAnyUserAsync(
                cancellationToken);

        if (environment.IsDevelopment() &&
            !hasUsers)
        {
            return RedirectToAction(
                nameof(Initialize));
        }

        ViewData["HasExistingUsers"] = hasUsers;

        return View(
            new LoginViewModel
            {
                ReturnUrl = returnUrl
            });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken = default)
    {
        ViewData["HasExistingUsers"] =
            await authenticationService.HasAnyUserAsync(
                cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result =
            await authenticationService.AuthenticateAsync(
                model.LoginName,
                model.Password,
                HttpContext.TraceIdentifier,
                cancellationToken);

        if (result.Status != AuthenticationStatus.Success ||
            result.User is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Status == AuthenticationStatus.Locked
                    ? "Konto jest czasowo zablokowane po kilku nieudanych próbach logowania. Spróbuj ponownie później."
                    : "Nieprawidłowy login lub hasło.");

            return View(model);
        }

        var user = result.User;

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()),
            new(
                ClaimTypes.Name,
                user.DisplayName),
            new(
                ClaimTypes.Role,
                user.RoleCode),
            new(
                "domio_login",
                user.LoginName),
            new(
                "domio_person_id",
                user.PersonId.ToString()),
            new(
                "domio_role_name",
                user.RoleNamePl)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) &&
            Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction(
            "Index",
            "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ForgotPassword(
        CancellationToken cancellationToken = default)
    {
        var hasUsers =
            await authenticationService.HasAnyUserAsync(
                cancellationToken);

        if (!hasUsers)
        {
            if (environment.IsDevelopment())
            {
                return RedirectToAction(
                    nameof(Initialize));
            }

            return RedirectToAction(
                nameof(Login));
        }

        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Initialize(
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        if (await authenticationService.HasAnyUserAsync(
                cancellationToken))
        {
            return RedirectToAction(
                nameof(Login));
        }

        return View(
            new InitializeAdministratorViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Initialize(
        InitializeAdministratorViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        ValidatePasswordComplexity(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await authenticationService
                .InitializeFirstAdministratorAsync(
                    new FirstAdministratorSetupRequest(
                        model.FirstName,
                        model.LastName,
                        model.LoginName,
                        model.Password),
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

        TempData["AccountMessage"] =
            "Konto administratora zostało utworzone. Możesz się teraz zalogować.";

        return RedirectToAction(
            nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken = default)
    {
        var userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (Guid.TryParse(
                userIdValue,
                out var userId))
        {
            await authenticationService.RecordLogoutAsync(
                userId,
                HttpContext.TraceIdentifier,
                cancellationToken);
        }

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(
            nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() =>
        View();

    private void ValidatePasswordComplexity(
        InitializeAdministratorViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            return;
        }

        if (!model.Password.Any(char.IsUpper) ||
            !model.Password.Any(char.IsLower) ||
            !model.Password.Any(char.IsDigit))
        {
            ModelState.AddModelError(
                nameof(model.Password),
                "Hasło musi zawierać małą literę, wielką literę i cyfrę.");
        }
    }
}
