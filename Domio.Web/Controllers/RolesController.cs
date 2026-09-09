using System.Security.Claims;
using Domio.Application.Users;
using Domio.Domain.Users;
using Domio.Web.Models.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

public sealed class RolesController(
    IRoleDirectoryService roleDirectoryService) : Controller
{
    [Authorize(Policy = SystemPermissions.RolesView)]
    [HttpGet]
    public async Task<IActionResult> Index(
        int? roleId,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await roleDirectoryService.GetOverviewAsync(
                cancellationToken);

        if (overview.Roles.Count == 0)
        {
            return View(overview);
        }

        var selectedRole =
            roleId.HasValue
                ? overview.Roles
                    .SingleOrDefault(
                        x => x.Id == roleId.Value)
                : overview.Roles[0];

        selectedRole ??= overview.Roles[0];

        ViewData["SelectedRoleId"] =
            selectedRole.Id;

        return View(overview);
    }

    [Authorize(Policy = SystemPermissions.RolesEdit)]
    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken = default)
    {
        var data =
            await roleDirectoryService.GetEditDataAsync(
                id,
                cancellationToken);

        if (data is null)
        {
            return NotFound();
        }

        return View(BuildModel(data));
    }

    [Authorize(Policy = SystemPermissions.RolesEdit)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EditRoleViewModel model,
        CancellationToken cancellationToken = default)
    {
        var current =
            await roleDirectoryService.GetEditDataAsync(
                model.RoleId,
                cancellationToken);

        if (current is null)
        {
            return NotFound();
        }

        RebuildMetadata(model, current);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var selections =
                model.Permissions
                    .Where(x => x.IsAssigned)
                    .Select(x =>
                        new RolePermissionSelection(
                            x.Code,
                            x.ScopeCode))
                    .ToArray();

            await roleDirectoryService.UpdateRoleAsync(
                new UpdateRoleRequest(
                    model.RoleId,
                    model.NamePl,
                    model.DescriptionPl,
                    selections),
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

        TempData["RolesMessage"] =
            "Rola i jej uprawnienia zostały zapisane.";

        return RedirectToAction(
            nameof(Index),
            new { roleId = model.RoleId });
    }

    [Authorize(Policy = SystemPermissions.RolesEdit)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPermissions(
        int id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await roleDirectoryService.ResetRolePermissionsAsync(
                id,
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["RolesError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["RolesMessage"] ??=
            "Przywrócono domyślne uprawnienia roli.";

        return RedirectToAction(nameof(Edit), new { id });
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

    private static EditRoleViewModel BuildModel(
        RoleEditData data) =>
        new()
        {
            RoleId = data.RoleId,
            Code = data.Code,
            NamePl = data.NamePl,
            DescriptionPl = data.DescriptionPl,
            IsSystem = data.IsSystem,
            UsesDefaultPermissions =
                data.UsesDefaultPermissions,
            Permissions =
                data.Permissions
                    .Select(BuildPermissionModel)
                    .ToList()
        };

    private static EditRolePermissionViewModel
        BuildPermissionModel(
            RoleEditPermissionData permission) =>
        new()
        {
            Code = permission.Code,
            ModuleCode = permission.ModuleCode,
            ModuleNamePl = permission.ModuleNamePl,
            NamePl = permission.NamePl,
            DescriptionPl = permission.DescriptionPl,
            RiskLevel = permission.RiskLevel,
            IsAssigned = permission.IsAssigned,
            ScopeCode = permission.ScopeCode,
            IsProtected = permission.IsProtected,
            ScopeOptions =
                permission.AllowedScopeCodes
                    .Select(scope =>
                        new SelectListItem
                        {
                            Value = scope,
                            Text =
                                PermissionScopes
                                    .GetNamePl(scope),
                            Selected =
                                scope ==
                                permission.ScopeCode
                        })
                    .ToList()
        };

    private static void RebuildMetadata(
        EditRoleViewModel model,
        RoleEditData current)
    {
        model.Code = current.Code;
        model.IsSystem = current.IsSystem;
        model.UsesDefaultPermissions =
            current.UsesDefaultPermissions;

        var posted =
            model.Permissions
                .ToDictionary(
                    x => x.Code,
                    StringComparer.Ordinal);

        model.Permissions =
            current.Permissions
                .Select(permission =>
                {
                    posted.TryGetValue(
                        permission.Code,
                        out var submitted);

                    var rebuilt =
                        BuildPermissionModel(permission);

                    if (submitted is not null)
                    {
                        rebuilt.IsAssigned =
                            submitted.IsAssigned;

                        if (permission.AllowedScopeCodes.Contains(
                                submitted.ScopeCode,
                                StringComparer.Ordinal))
                        {
                            rebuilt.ScopeCode =
                                submitted.ScopeCode;
                        }
                    }

                    if (rebuilt.IsProtected)
                    {
                        rebuilt.IsAssigned = true;
                    }

                    return rebuilt;
                })
                .ToList();
    }
}
