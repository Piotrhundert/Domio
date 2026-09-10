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
public sealed class HouseholdFinanceController(
    IHouseholdFinanceService householdFinanceService) : Controller
{
    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdView)]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await householdFinanceService
                .GetOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        return View(
            new HouseholdFinanceIndexViewModel
            {
                Overview =
                    overview
            });
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public IActionResult CreateAccount()
    {
        var model =
            new CreateHouseholdAccountViewModel
            {
                AccountTypeCode =
                    HouseholdAccountTypes.Bank,
                CurrencyCode =
                    "PLN",
                InitialBalance =
                    0m
            };

        RebuildAccountTypeOptions(
            model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CreateAccount(
        CreateHouseholdAccountViewModel model,
        CancellationToken cancellationToken = default)
    {
        RebuildAccountTypeOptions(
            model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await householdFinanceService
                .CreateAccountAsync(
                    new CreateHouseholdAccountRequest(
                        model.Name,
                        model.AccountTypeCode,
                        model.CurrencyCode,
                        model.InitialBalance),
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

        TempData["HouseholdFinanceMessage"] =
            "Konto gospodarstwa zostało utworzone.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> AddOperation(
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await householdFinanceService
                .GetOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        if (overview is null ||
            overview.Accounts.All(x =>
                !x.IsActive))
        {
            TempData["HouseholdFinanceMessage"] =
                "Najpierw utwórz aktywne konto gospodarstwa.";

            return RedirectToAction(
                nameof(CreateAccount));
        }

        var activeAccounts =
            overview.Accounts
                .Where(x =>
                    x.IsActive)
                .ToArray();

        var selectedAccountId =
            accountId.HasValue &&
            activeAccounts.Any(x =>
                x.AccountId ==
                    accountId.Value)
                ? accountId.Value
                : activeAccounts[0].AccountId;

        var model =
            new AddHouseholdOperationViewModel
            {
                AccountId =
                    selectedAccountId,
                EntryTypeCode =
                    HouseholdEntryTypes.Expense,
                Amount =
                    0.01m,
                OccurredOn =
                    DateTime.Today,
                CategoryCode =
                    HouseholdFinanceCategories.OtherExpense
            };

        RebuildOperationOptions(
            model,
            activeAccounts);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> AddOperation(
        AddHouseholdOperationViewModel model,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await householdFinanceService
                .GetOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        var activeAccounts =
            overview?.Accounts
                .Where(x =>
                    x.IsActive)
                .ToArray()
            ?? [];

        RebuildOperationOptions(
            model,
            activeAccounts);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await householdFinanceService
                .PostOperationAsync(
                    new PostHouseholdOperationRequest(
                        model.AccountId,
                        model.EntryTypeCode,
                        model.Amount,
                        DateTime.SpecifyKind(
                            model.OccurredOn.Date
                                .AddHours(12),
                            DateTimeKind.Utc),
                        model.CategoryCode,
                        model.Description,
                        "Manual",
                        null),
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

        TempData["HouseholdFinanceMessage"] =
            "Operacja finansów domu została zaksięgowana.";

        return RedirectToAction(
            nameof(Index));
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new InvalidOperationException(
                "Nie można ustalić zalogowanego użytkownika.");
        }

        return userId;
    }

    private static void RebuildAccountTypeOptions(
        CreateHouseholdAccountViewModel model)
    {
        model.AccountTypes =
            HouseholdAccountTypes.All
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.Code,
                        Text =
                            x.NamePl,
                        Selected =
                            x.Code ==
                            model.AccountTypeCode
                    })
                .ToList();
    }

    private static void RebuildOperationOptions(
        AddHouseholdOperationViewModel model,
        IReadOnlyList<HouseholdAccountSummary> accounts)
    {
        model.Accounts =
            accounts
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.AccountId.ToString(),
                        Text =
                            $"{x.Name} · {x.Balance:N2} {x.CurrencyCode}",
                        Selected =
                            x.AccountId ==
                            model.AccountId
                    })
                .ToList();

        model.EntryTypes =
        [
            new SelectListItem
            {
                Value =
                    HouseholdEntryTypes.Expense,
                Text =
                    "Wydatek",
                Selected =
                    model.EntryTypeCode ==
                    HouseholdEntryTypes.Expense
            },
            new SelectListItem
            {
                Value =
                    HouseholdEntryTypes.Income,
                Text =
                    "Wpływ",
                Selected =
                    model.EntryTypeCode ==
                    HouseholdEntryTypes.Income
            }
        ];

        model.Categories =
            HouseholdFinanceCategories.All
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.Code,
                        Text =
                            x.NamePl,
                        Selected =
                            x.Code ==
                            model.CategoryCode
                    })
                .ToList();
    }
}
