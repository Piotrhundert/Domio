using System.Security.Claims;
using Domio.Application.HouseholdFinance;
using Domio.Domain.HouseholdFinance;
using Domio.Domain.Users;
using Domio.Web.Models.HouseholdFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize]
[Route("HouseholdFinance/ContributionAdmin")]
public sealed class HouseholdContributionAdminController(
    IHouseholdContributionAdminService adminService) : Controller
{
    [HttpGet("")]
    [Authorize(Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await adminService.GetOverviewAsync(
                GetCurrentUserId(),
                cancellationToken);

        if (overview is null)
        {
            TempData["HouseholdFinanceError"] =
                "Najpierw utwórz i skonfiguruj gospodarstwo domowe.";

            return RedirectToAction(
                "Index",
                "HouseholdFinance");
        }

        return View(
            new HouseholdContributionAdminIndexViewModel
            {
                Overview = overview
            });
    }

    [HttpGet("Edit/{id:guid}")]
    [Authorize(Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> EditRule(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var data =
            await adminService.GetRuleForEditAsync(
                id,
                GetCurrentUserId(),
                cancellationToken);

        if (data is null)
        {
            TempData["HouseholdFinanceError"] =
                "Nie znaleziono aktywnej reguły składki.";

            return RedirectToAction(nameof(Index));
        }

        return View(BuildEditModel(data));
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> EditRule(
        Guid id,
        EditHouseholdContributionRuleViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (id != model.RuleId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            var reload =
                await adminService.GetRuleForEditAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

            if (reload is null)
            {
                return NotFound();
            }

            RebuildEditOptions(model, reload);
            return View(model);
        }

        try
        {
            var result =
                await adminService.UpdateRuleAsync(
                    new UpdateHouseholdContributionRuleRequest(
                        model.RuleId,
                        model.ModeCode,
                        model.FixedAmount,
                        model.Percentage,
                        model.DueOffsetDays,
                        model.TargetHouseholdAccountId,
                        model.ValidTo.HasValue
                            ? DateTime.SpecifyKind(
                                model.ValidTo.Value.Date,
                                DateTimeKind.Utc)
                            : null,
                        model.ReminderDays),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            TempData["HouseholdFinanceMessage"] =
                $"Reguła została zmieniona od {result.EffectiveFromUtc:MM.yyyy}. Przeliczono {result.ObligationsRecalculated} przyszłych zobowiązań" +
                (result.ObligationsCancelled > 0
                    ? $", anulowano {result.ObligationsCancelled} zobowiązań poza nowym okresem."
                    : ".");
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadEditViewAsync(model, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadEditViewAsync(model, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Child/New")]
    [Authorize(Policy = SystemPermissions.FinanceHouseholdManage)]
    public IActionResult CreateChild()
    {
        return View(new CreateHouseholdChildViewModel());
    }

    [HttpPost("Child/New")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CreateChild(
        CreateHouseholdChildViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await adminService.CreateChildAsync(
                new CreateHouseholdChildRequest(
                    model.FirstName,
                    model.LastName,
                    model.DisplayName),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["HouseholdFinanceMessage"] =
            "Dziecko zostało dodane do gospodarstwa bez tworzenia konta użytkownika.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Child/{householdMemberId:guid}/Contribution/New")]
    [Authorize(Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CreateChildContribution(
        Guid householdMemberId,
        CancellationToken cancellationToken = default)
    {
        var data =
            await adminService.GetChildContributionFormAsync(
                householdMemberId,
                GetCurrentUserId(),
                cancellationToken);

        if (data is null)
        {
            TempData["HouseholdFinanceError"] =
                "Nie można dodać składki dla tego dziecka albo dziecko ma już aktywną regułę.";

            return RedirectToAction(nameof(Index));
        }

        if (data.TargetAccounts.Count == 0)
        {
            TempData["HouseholdFinanceError"] =
                "Najpierw utwórz aktywne konto gospodarstwa, na które ma trafiać składka.";

            return RedirectToAction(nameof(Index));
        }

        var today = DateTime.Today;
        var model =
            new CreateHouseholdChildContributionViewModel
            {
                HouseholdMemberId = data.HouseholdMemberId,
                ChildName = data.ChildName,
                CurrencyCode = data.CurrencyCode,
                FixedAmount = 0.01m,
                DueDay = Math.Clamp(today.Day, 1, 28),
                TargetHouseholdAccountId = data.TargetAccounts[0].AccountId,
                ValidFrom = today,
                ReminderDays = 3
            };

        RebuildChildContributionOptions(model, data);
        return View(model);
    }

    [HttpPost("Child/{householdMemberId:guid}/Contribution/New")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CreateChildContribution(
        Guid householdMemberId,
        CreateHouseholdChildContributionViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (householdMemberId != model.HouseholdMemberId)
        {
            return BadRequest();
        }

        var data =
            await adminService.GetChildContributionFormAsync(
                householdMemberId,
                GetCurrentUserId(),
                cancellationToken);

        if (data is null)
        {
            TempData["HouseholdFinanceError"] =
                "Nie można dodać składki dla tego dziecka albo dziecko ma już aktywną regułę.";

            return RedirectToAction(nameof(Index));
        }

        RebuildChildContributionOptions(model, data);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await adminService.CreateChildContributionAsync(
                new CreateHouseholdChildContributionRequest(
                    model.HouseholdMemberId,
                    model.FixedAmount,
                    model.DueDay,
                    model.TargetHouseholdAccountId,
                    DateTime.SpecifyKind(
                        model.ValidFrom.Date,
                        DateTimeKind.Utc),
                    model.ValidTo.HasValue
                        ? DateTime.SpecifyKind(
                            model.ValidTo.Value.Date,
                            DateTimeKind.Utc)
                        : null,
                    model.ReminderDays),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["HouseholdFinanceMessage"] =
            $"Dodano miesięczną składkę dla {data.ChildName}.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ReloadEditViewAsync(
        EditHouseholdContributionRuleViewModel model,
        CancellationToken cancellationToken)
    {
        var reload =
            await adminService.GetRuleForEditAsync(
                model.RuleId,
                GetCurrentUserId(),
                cancellationToken);

        if (reload is null)
        {
            return NotFound();
        }

        RebuildEditOptions(model, reload);
        return View("EditRule", model);
    }

    private static EditHouseholdContributionRuleViewModel BuildEditModel(
        HouseholdContributionRuleEditData data)
    {
        var model =
            new EditHouseholdContributionRuleViewModel
            {
                RuleId = data.RuleId,
                HouseholdMemberName = data.HouseholdMemberName,
                ModeCode = data.ModeCode,
                FixedAmount = data.FixedAmount,
                Percentage = data.Percentage,
                DueOffsetDays = data.DueOffsetDays,
                TargetHouseholdAccountId = data.TargetHouseholdAccountId,
                ValidTo = data.ValidToUtc?.ToLocalTime().Date,
                ReminderDays = data.ReminderDays,
                CurrentValidFrom = data.ValidFromUtc,
                IncomeRuleName = data.IncomeRuleName,
                PlannedIncomeAmount = data.PlannedIncomeAmount,
                CurrencyCode = data.CurrencyCode,
                IsChildContribution = data.IsChildContribution
            };

        RebuildEditOptions(model, data);
        return model;
    }

    private static void RebuildEditOptions(
        EditHouseholdContributionRuleViewModel model,
        HouseholdContributionRuleEditData data)
    {
        model.HouseholdMemberName = data.HouseholdMemberName;
        model.CurrentValidFrom = data.ValidFromUtc;
        model.IncomeRuleName = data.IncomeRuleName;
        model.PlannedIncomeAmount = data.PlannedIncomeAmount;
        model.CurrencyCode = data.CurrencyCode;
        model.IsChildContribution = data.IsChildContribution;

        model.Modes =
            (data.IsChildContribution
                ? HouseholdContributionModes.All.Where(x =>
                    x.Code == HouseholdContributionModes.FixedAmount)
                : HouseholdContributionModes.All)
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code,
                        x.Code == model.ModeCode))
                .ToList();

        model.TargetAccounts =
            data.TargetAccounts
                .Select(x =>
                    new SelectListItem(
                        $"{x.Name} · {x.CurrencyCode}",
                        x.AccountId.ToString(),
                        x.AccountId == model.TargetHouseholdAccountId))
                .ToList();
    }

    private static void RebuildChildContributionOptions(
        CreateHouseholdChildContributionViewModel model,
        HouseholdChildContributionFormData data)
    {
        model.HouseholdMemberId = data.HouseholdMemberId;
        model.ChildName = data.ChildName;
        model.CurrencyCode = data.CurrencyCode;

        model.TargetAccounts =
            data.TargetAccounts
                .Select(x =>
                    new SelectListItem(
                        $"{x.Name} · {x.CurrencyCode}",
                        x.AccountId.ToString(),
                        x.AccountId == model.TargetHouseholdAccountId))
                .ToList();
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Nie można ustalić zalogowanego użytkownika.");
    }
}
