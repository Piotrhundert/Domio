using System.Security.Claims;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Web.Models.FamilyFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class FamilyAreasController(
    IFamilyAreaService familyAreaService,
    IFamilyBudgetService familyBudgetService) : Controller
{
    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> Index(
        Guid familyGroupId,
        int? year,
        int? month,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        try
        {
            // Wywołanie budżetu generuje brakujące wystąpienia istniejących
            // reguł rodzinnych dla wybranego miesiąca.
            await familyBudgetService.GetOverviewAsync(
                familyGroupId,
                GetCurrentUserId(),
                selectedYear,
                selectedMonth,
                cancellationToken);

            var overview =
                await familyAreaService.GetOverviewAsync(
                    familyGroupId,
                    GetCurrentUserId(),
                    selectedYear,
                    selectedMonth,
                    cancellationToken);

            return View(overview);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            TempData["FamilyAreaError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Index),
                new { familyGroupId });
        }
    }

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> Details(
        Guid id,
        int? year,
        int? month,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        try
        {
            var preview =
                await familyAreaService.GetDetailsAsync(
                    id,
                    GetCurrentUserId(),
                    selectedYear,
                    selectedMonth,
                    cancellationToken);

            if (preview is null)
            {
                return NotFound();
            }

            await familyBudgetService.GetOverviewAsync(
                preview.FamilyGroupId,
                GetCurrentUserId(),
                selectedYear,
                selectedMonth,
                cancellationToken);

            var details =
                await familyAreaService.GetDetailsAsync(
                    id,
                    GetCurrentUserId(),
                    selectedYear,
                    selectedMonth,
                    cancellationToken);

            return details is null
                ? NotFound()
                : View(details);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            TempData["FamilyAreaError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Index));
        }
    }

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> Create(
        Guid familyGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var overview =
                await familyAreaService.GetOverviewAsync(
                    familyGroupId,
                    GetCurrentUserId(),
                    today.Year,
                    today.Month,
                    cancellationToken);

            return View(
                new CreateFamilyAreaViewModel
                {
                    FamilyGroupId =
                        overview.FamilyGroupId,
                    FamilyGroupName =
                        overview.FamilyGroupName
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> Create(
        CreateFamilyAreaViewModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var overview =
                await familyAreaService.GetOverviewAsync(
                    model.FamilyGroupId,
                    GetCurrentUserId(),
                    today.Year,
                    today.Month,
                    cancellationToken);

            model.FamilyGroupName =
                overview.FamilyGroupName;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var areaId =
                await familyAreaService.CreateAreaAsync(
                    new CreateFamilyAreaRequest(
                        model.FamilyGroupId,
                        model.Name),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            TempData["FamilyAreaMessage"] =
                "Obszar został dodany. Sam obszar nie tworzy żadnego wydatku.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = areaId,
                    year = today.Year,
                    month = today.Month
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

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> CreateItem(
        Guid areaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context =
                await familyAreaService.GetCreateItemContextAsync(
                    areaId,
                    GetCurrentUserId(),
                    cancellationToken);

            if (context is null)
            {
                return NotFound();
            }

            var today = DateTime.Today;
            var model =
                new CreateFamilyAreaItemViewModel
                {
                    FamilyGroupId =
                        context.FamilyGroupId,
                    FamilyGroupName =
                        context.FamilyGroupName,
                    AreaId =
                        context.AreaId,
                    AreaName =
                        context.AreaName,
                    CategoryCode =
                        DefaultCategoryForArea(
                            context.AreaName),
                    FrequencyCode =
                        FamilyRecurringFrequencies.Monthly,
                    DueDateModeCode =
                        FamilyRecurringDueDateModes.SpecificDay,
                    DueDay =
                        Math.Min(
                            today.Day,
                            28),
                    ActiveFromUtc =
                        today
                };

            RebuildItemOptions(
                model,
                context);

            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyAreaError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> CreateItem(
        CreateFamilyAreaItemViewModel model,
        CancellationToken cancellationToken = default)
    {
        CreateFamilyAreaItemContext? context;

        try
        {
            context =
                await familyAreaService.GetCreateItemContextAsync(
                    model.AreaId,
                    GetCurrentUserId(),
                    cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyAreaError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId =
                        model.FamilyGroupId
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (context is null)
        {
            return NotFound();
        }

        model.FamilyGroupId =
            context.FamilyGroupId;
        model.FamilyGroupName =
            context.FamilyGroupName;
        model.AreaName =
            context.AreaName;

        RebuildItemOptions(
            model,
            context);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyAreaService.CreateItemAsync(
                new CreateFamilyAreaItemRequest(
                    model.AreaId,
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
                $"Dodano pozycję „{model.Name}” do obszaru {model.AreaName}. To ta sama reguła kosztu, która zasila budżet rodzinny.";

            var period =
                model.ActiveFromUtc == default
                    ? DateTime.Today
                    : model.ActiveFromUtc;

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = model.AreaId,
                    year = period.Year,
                    month = period.Month
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

    private static void RebuildItemOptions(
        CreateFamilyAreaItemViewModel model,
        CreateFamilyAreaItemContext context)
    {
        model.Categories =
            FamilyBudgetCategories.All
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code,
                        x.Code ==
                            model.CategoryCode))
                .ToList();

        model.Frequencies =
            FamilyRecurringFrequencies.All
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code,
                        x.Code ==
                            model.FrequencyCode))
                .ToList();

        model.DueDateModes =
            FamilyRecurringDueDateModes.All
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code,
                        x.Code ==
                            model.DueDateModeCode))
                .ToList();

        model.Beneficiaries =
        [
            new SelectListItem(
                "— koszt wspólny rodziny —",
                string.Empty,
                !model.BeneficiaryPersonId.HasValue),
            .. context.Beneficiaries
                .Select(x =>
                    new SelectListItem(
                        $"{x.DisplayName} ({FamilyRoles.GetNamePl(x.FamilyRoleCode)})",
                        x.PersonId.ToString(),
                        x.PersonId ==
                            model.BeneficiaryPersonId))
        ];
    }

    private static string DefaultCategoryForArea(
        string areaName) =>
        areaName.Equals(
            "Przedszkole",
            StringComparison.OrdinalIgnoreCase)
            ? FamilyBudgetCategories.Education
            : areaName.Equals(
                "Auto",
                StringComparison.OrdinalIgnoreCase)
                ? FamilyBudgetCategories.Transport
                : FamilyBudgetCategories.Other;

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
