using System.Security.Claims;
using Domio.Application.Reporting;
using Domio.Web.Models.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class FinanceReportsController(
    IFinanceReportingService financeReportingService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? scope,
        int months = 12,
        string? currency = "PLN",
        Guid? familyGroupId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dashboard = await financeReportingService.GetDashboardAsync(
                GetCurrentUserId(),
                new FinanceReportQuery(
                    scope,
                    months,
                    currency,
                    familyGroupId),
                cancellationToken);

            return View(
                new FinanceReportPageViewModel
                {
                    Dashboard = dashboard,
                    Simulation = BuildDefaultSimulation(dashboard)
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            TempData["FinanceReportError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(
        FinanceSimulationViewModel model,
        CancellationToken cancellationToken = default)
    {
        FinanceReportDashboard dashboard;

        try
        {
            dashboard = await financeReportingService.GetDashboardAsync(
                GetCurrentUserId(),
                new FinanceReportQuery(
                    model.ScopeCode,
                    model.HistoryMonths,
                    model.CurrencyCode,
                    model.FamilyGroupId),
                cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        model.ScopeCode = dashboard.ScopeCode;
        model.FamilyGroupId = dashboard.FamilyGroupId;
        model.CurrencyCode = dashboard.CurrencyCode;
        model.HistoryMonths = dashboard.Months;

        if (model.OneTimeMonth > model.HorizonMonths)
        {
            ModelState.AddModelError(
                nameof(model.OneTimeMonth),
                "Miesiąc zdarzenia jednorazowego nie może wykraczać poza horyzont symulacji.");
        }

        if (!ModelState.IsValid)
        {
            return View(
                "Index",
                new FinanceReportPageViewModel
                {
                    Dashboard = dashboard,
                    Simulation = model
                });
        }

        try
        {
            var result = await financeReportingService.SimulateAsync(
                GetCurrentUserId(),
                model.ToRequest(),
                cancellationToken);

            return View(
                "Index",
                new FinanceReportPageViewModel
                {
                    Dashboard = dashboard,
                    Simulation = model,
                    SimulationResult = result
                });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return View(
            "Index",
            new FinanceReportPageViewModel
            {
                Dashboard = dashboard,
                Simulation = model
            });
    }

    private static FinanceSimulationViewModel BuildDefaultSimulation(
        FinanceReportDashboard dashboard) =>
        new()
        {
            ScopeCode = dashboard.ScopeCode,
            FamilyGroupId = dashboard.FamilyGroupId,
            CurrencyCode = dashboard.CurrencyCode,
            HistoryMonths = dashboard.Months,
            HorizonMonths = 12,
            OneTimeMonth = 1,
            TargetReserve = Math.Max(0m, dashboard.Kpis.AverageMonthlyExpense * 3m)
        };

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Brak identyfikatora zalogowanego użytkownika.");
    }
}
