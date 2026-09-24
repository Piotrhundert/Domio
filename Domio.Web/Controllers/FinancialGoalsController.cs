using System.Security.Claims;
using Domio.Application.FinancialGoals;
using Domio.Domain.FinancialGoals;
using Domio.Web.Models.FinancialGoals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class FinancialGoalsController(
    IFinancialGoalService financialGoalService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string scope = FinancialGoalScopes.Personal,
        Guid? familyGroupId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var overview = await financialGoalService.GetOverviewAsync(
                scope,
                familyGroupId,
                GetCurrentUserId(),
                cancellationToken);

            return View(
                new FinancialGoalIndexViewModel
                {
                    Overview = overview
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            TempData["FinancialGoalError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { scope = FinancialGoalScopes.Personal });
        }
        catch (InvalidOperationException exception)
        {
            TempData["FinancialGoalError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { scope = FinancialGoalScopes.Personal });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        string scope,
        Guid? familyGroupId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var overview = await financialGoalService.GetOverviewAsync(
                scope,
                familyGroupId,
                GetCurrentUserId(),
                cancellationToken);

            if (!overview.Scope.CanManage)
            {
                return Forbid();
            }

            var model = new FinancialGoalFormViewModel
            {
                ScopeCode = overview.Scope.ScopeCode,
                FamilyGroupId = overview.Scope.FamilyGroupId,
                ScopeNamePl = overview.Scope.ScopeNamePl,
                OwnerDisplayName = overview.Scope.OwnerDisplayName,
                CategoryCode = FinancialGoalCategories.Other,
                TargetDateUtc = DateTime.Today.AddMonths(12)
            };

            RebuildCategoryOptions(model);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            TempData["FinancialGoalError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { scope, familyGroupId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        FinancialGoalFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        FinancialGoalOverview? overview = null;

        try
        {
            overview = await financialGoalService.GetOverviewAsync(
                model.ScopeCode,
                model.FamilyGroupId,
                GetCurrentUserId(),
                cancellationToken);

            model.ScopeNamePl = overview.Scope.ScopeNamePl;
            model.OwnerDisplayName = overview.Scope.OwnerDisplayName;
            model.FamilyGroupId = overview.Scope.FamilyGroupId;
            RebuildCategoryOptions(model);

            if (!overview.Scope.CanManage)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await financialGoalService.CreateAsync(
                new CreateFinancialGoalRequest(
                    model.ScopeCode,
                    model.FamilyGroupId,
                    model.Name,
                    model.CategoryCode,
                    model.TargetAmount,
                    model.TargetDateUtc,
                    model.Notes,
                    model.InitialAmount),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FinancialGoalMessage"] =
                "Cel finansowy został utworzony. Odłożona kwota jest rezerwą planistyczną i nie zmienia salda rachunku.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    scope = model.ScopeCode,
                    familyGroupId = model.FamilyGroupId
                });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        RebuildCategoryOptions(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var details = await financialGoalService.GetDetailsAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

            if (details is null)
            {
                return NotFound();
            }

            return View(
                new FinancialGoalDetailsViewModel
                {
                    Details = details
                });
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
            var details = await financialGoalService.GetDetailsAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

            if (details is null)
            {
                return NotFound();
            }

            if (!details.Scope.CanManage || !details.Goal.IsActive)
            {
                return Forbid();
            }

            var model = new FinancialGoalFormViewModel
            {
                GoalId = details.Goal.GoalId,
                ScopeCode = details.Scope.ScopeCode,
                FamilyGroupId = details.Scope.FamilyGroupId,
                ScopeNamePl = details.Scope.ScopeNamePl,
                OwnerDisplayName = details.Scope.OwnerDisplayName,
                Name = details.Goal.Name,
                CategoryCode = details.Goal.CategoryCode,
                TargetAmount = details.Goal.TargetAmount,
                TargetDateUtc = details.Goal.TargetDateUtc,
                Notes = details.Goal.Notes
            };

            RebuildCategoryOptions(model);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        FinancialGoalFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!model.GoalId.HasValue)
        {
            return BadRequest();
        }

        try
        {
            var details = await financialGoalService.GetDetailsAsync(
                model.GoalId.Value,
                GetCurrentUserId(),
                cancellationToken);

            if (details is null)
            {
                return NotFound();
            }

            model.ScopeCode = details.Scope.ScopeCode;
            model.FamilyGroupId = details.Scope.FamilyGroupId;
            model.ScopeNamePl = details.Scope.ScopeNamePl;
            model.OwnerDisplayName = details.Scope.OwnerDisplayName;
            RebuildCategoryOptions(model);

            if (!details.Scope.CanManage)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await financialGoalService.UpdateAsync(
                new UpdateFinancialGoalRequest(
                    model.GoalId.Value,
                    model.Name,
                    model.CategoryCode,
                    model.TargetAmount,
                    model.TargetDateUtc,
                    model.Notes),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FinancialGoalMessage"] = "Cel finansowy został zaktualizowany.";

            return RedirectToAction(nameof(Details), new { id = model.GoalId.Value });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        RebuildCategoryOptions(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> AddContribution(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var details = await financialGoalService.GetDetailsAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

            if (details is null)
            {
                return NotFound();
            }

            if (!details.Scope.CanContribute || !details.Goal.IsActive)
            {
                return Forbid();
            }

            return View(
                BuildContributionViewModel(details));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContribution(
        FinancialGoalContributionViewModel model,
        CancellationToken cancellationToken = default)
    {
        FinancialGoalDetails? details;

        try
        {
            details = await financialGoalService.GetDetailsAsync(
                model.GoalId,
                GetCurrentUserId(),
                cancellationToken);

            if (details is null)
            {
                return NotFound();
            }

            RebuildContributionViewModel(model, details);

            if (!details.Scope.CanContribute)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await financialGoalService.AddContributionAsync(
                new AddFinancialGoalContributionRequest(
                    model.GoalId,
                    model.Amount,
                    model.ContributedAtUtc,
                    model.Note),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FinancialGoalMessage"] =
                "Wpłata do celu została zapisana jako odłożona/rezerwowana kwota. Saldo rachunku nie zostało zmienione.";

            return RedirectToAction(nameof(Details), new { id = model.GoalId });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var details = await financialGoalService.GetDetailsAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

            if (details is null)
            {
                return NotFound();
            }

            await financialGoalService.ArchiveAsync(
                id,
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FinancialGoalMessage"] =
                "Cel został zarchiwizowany. Historia wpłat została zachowana.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    scope = details.Scope.ScopeCode,
                    familyGroupId = details.Scope.FamilyGroupId
                });
        }
        catch (InvalidOperationException exception)
        {
            TempData["FinancialGoalError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private static FinancialGoalContributionViewModel BuildContributionViewModel(
        FinancialGoalDetails details)
    {
        var model = new FinancialGoalContributionViewModel
        {
            GoalId = details.Goal.GoalId,
            GoalName = details.Goal.Name,
            ScopeCode = details.Scope.ScopeCode,
            FamilyGroupId = details.Scope.FamilyGroupId,
            ScopeNamePl = details.Scope.ScopeNamePl,
            TargetAmount = details.Goal.TargetAmount,
            SavedAmount = details.Goal.SavedAmount,
            RemainingAmount = details.Goal.RemainingAmount,
            Amount = details.Goal.RemainingAmount > 0m
                ? details.Goal.RemainingAmount
                : 0m,
            ContributedAtUtc = DateTime.Today
        };

        return model;
    }

    private static void RebuildContributionViewModel(
        FinancialGoalContributionViewModel model,
        FinancialGoalDetails details)
    {
        model.GoalName = details.Goal.Name;
        model.ScopeCode = details.Scope.ScopeCode;
        model.FamilyGroupId = details.Scope.FamilyGroupId;
        model.ScopeNamePl = details.Scope.ScopeNamePl;
        model.TargetAmount = details.Goal.TargetAmount;
        model.SavedAmount = details.Goal.SavedAmount;
        model.RemainingAmount = details.Goal.RemainingAmount;
    }

    private static void RebuildCategoryOptions(
        FinancialGoalFormViewModel model)
    {
        model.Categories = FinancialGoalCategories.All
            .Select(x => new SelectListItem(
                x.NamePl,
                x.Code,
                x.Code == model.CategoryCode))
            .ToList();
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Brak identyfikatora zalogowanego użytkownika.");
    }
}
