using System.Security.Claims;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Web.Models.FamilyFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class FamilyAreaItemsController(
    IFamilyAreaItemManagementService itemManagementService) : Controller
{
    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> Edit(
        Guid ruleId,
        int? year,
        int? month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context =
                await itemManagementService.GetEditContextAsync(
                    ruleId,
                    GetCurrentUserId(),
                    cancellationToken);

            if (context is null)
            {
                return NotFound();
            }

            if (!context.IsActive)
            {
                TempData["FamilyAreaError"] =
                    "Zakończonej pozycji nie można edytować.";

                return RedirectToAction(
                    "Details",
                    "FamilyAreas",
                    new
                    {
                        id = context.AreaId,
                        year,
                        month
                    });
            }

            var today = DateTime.Today;
            var model = new EditFamilyAreaItemViewModel
            {
                FamilyGroupId = context.FamilyGroupId,
                FamilyGroupName = context.FamilyGroupName,
                AreaId = context.AreaId,
                AreaName = context.AreaName,
                RuleId = context.RuleId,
                Year = year ?? today.Year,
                Month = month ?? today.Month,
                Name = context.Name,
                CategoryCode = context.CategoryCode,
                PlannedAmount = context.PlannedAmount,
                FrequencyCode = context.FrequencyCode,
                DueDateModeCode = context.DueDateModeCode,
                DueDay = context.DueDay,
                DaysBeforeEnd = context.DaysBeforeEnd,
                BeneficiaryPersonId = context.BeneficiaryPersonId,
                ActiveFromUtc = context.ActiveFromUtc,
                ActiveToUtc = context.ActiveToUtc
            };

            RebuildOptions(
                model,
                context.Beneficiaries);

            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyAreaError"] = exception.Message;
            return RedirectToAction("Index", "FamilyAreas");
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> Edit(
        EditFamilyAreaItemViewModel model,
        CancellationToken cancellationToken = default)
    {
        EditFamilyAreaItemContext? context;

        try
        {
            context =
                await itemManagementService.GetEditContextAsync(
                    model.RuleId,
                    GetCurrentUserId(),
                    cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyAreaError"] = exception.Message;
            return RedirectToAction(
                "Details",
                "FamilyAreas",
                new
                {
                    id = model.AreaId,
                    year = model.Year,
                    month = model.Month
                });
        }

        if (context is null)
        {
            return NotFound();
        }

        model.FamilyGroupId = context.FamilyGroupId;
        model.FamilyGroupName = context.FamilyGroupName;
        model.AreaId = context.AreaId;
        model.AreaName = context.AreaName;

        RebuildOptions(
            model,
            context.Beneficiaries);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await itemManagementService.UpdateAsync(
                new UpdateFamilyAreaItemRequest(
                    model.RuleId,
                    model.Name,
                    model.CategoryCode,
                    model.PlannedAmount,
                    model.FrequencyCode,
                    model.DueDateModeCode,
                    model.DueDay,
                    model.DaysBeforeEnd,
                    model.BeneficiaryPersonId,
                    model.ActiveFromUtc,
                    model.ActiveToUtc),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyAreaMessage"] =
                $"Zapisano zmiany pozycji „{model.Name}”. Opłacone wystąpienia historyczne pozostały bez zmian.";

            return RedirectToAction(
                "Details",
                "FamilyAreas",
                new
                {
                    id = model.AreaId,
                    year = model.Year,
                    month = model.Month
                });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> End(
        Guid ruleId,
        Guid areaId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await itemManagementService.EndAsync(
                ruleId,
                DateTime.Today,
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyAreaMessage"] =
                "Pozycja została zakończona. Historia i wcześniejsze płatności pozostały zachowane.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyAreaError"] = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["FamilyAreaError"] = exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            "Details",
            "FamilyAreas",
            new
            {
                id = areaId,
                year,
                month
            });
    }

    private static void RebuildOptions(
        EditFamilyAreaItemViewModel model,
        IReadOnlyList<FamilyAreaBeneficiary> beneficiaries)
    {
        model.Categories =
            FamilyBudgetCategories.All
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code,
                        x.Code == model.CategoryCode))
                .ToList();

        model.Frequencies =
            FamilyRecurringFrequencies.All
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code,
                        x.Code == model.FrequencyCode))
                .ToList();

        model.DueDateModes =
            FamilyRecurringDueDateModes.All
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code,
                        x.Code == model.DueDateModeCode))
                .ToList();

        model.Beneficiaries =
        [
            new SelectListItem(
                "— koszt wspólny rodziny —",
                string.Empty,
                !model.BeneficiaryPersonId.HasValue),
            .. beneficiaries.Select(x =>
                new SelectListItem(
                    $"{x.DisplayName} ({FamilyRoles.GetNamePl(x.FamilyRoleCode)})",
                    x.PersonId.ToString(),
                    x.PersonId == model.BeneficiaryPersonId))
        ];
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return Guid.TryParse(
            value,
            out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Brak identyfikatora zalogowanego użytkownika.");
    }
}
