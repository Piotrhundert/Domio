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

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> Transfer(
        Guid? sourceAccountId,
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

        if (activeAccounts.Length < 2)
        {
            TempData["HouseholdFinanceError"] =
                "Transfer wymaga co najmniej dwóch aktywnych kont gospodarstwa.";

            return RedirectToAction(
                nameof(Index));
        }

        var selectedSource =
            sourceAccountId.HasValue &&
            activeAccounts.Any(x =>
                x.AccountId ==
                    sourceAccountId.Value)
                ? sourceAccountId.Value
                : activeAccounts[0].AccountId;

        var selectedTarget =
            activeAccounts
                .First(x =>
                    x.AccountId !=
                        selectedSource)
                .AccountId;

        var model =
            new HouseholdTransferViewModel
            {
                SourceAccountId =
                    selectedSource,
                TargetAccountId =
                    selectedTarget,
                Amount =
                    0.01m,
                OccurredOn =
                    DateTime.Today
            };

        RebuildTransferOptions(
            model,
            activeAccounts);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> Transfer(
        HouseholdTransferViewModel model,
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

        RebuildTransferOptions(
            model,
            activeAccounts);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await householdFinanceService
                .TransferBetweenAccountsAsync(
                    new CreateHouseholdTransferRequest(
                        model.SourceAccountId,
                        model.TargetAccountId,
                        model.Amount,
                        DateTime.SpecifyKind(
                            model.OccurredOn.Date
                                .AddHours(12),
                            DateTimeKind.Utc),
                        model.Description),
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
            "Transfer pomiędzy kontami domu został wykonany.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CloseAccount(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var info =
            await householdFinanceService
                .GetAccountClosureInfoAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

        if (info is null)
        {
            return NotFound();
        }

        return View(
            new CloseHouseholdAccountViewModel
            {
                AccountId =
                    info.AccountId,
                Name =
                    info.Name,
                AccountTypeNamePl =
                    info.AccountTypeNamePl,
                CurrencyCode =
                    info.CurrencyCode,
                Balance =
                    info.Balance,
                IsActive =
                    info.IsActive
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CloseAccountConfirmed(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await householdFinanceService
                .CloseAccountAsync(
                    accountId,
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;

            return RedirectToAction(
                nameof(CloseAccount),
                new { id = accountId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["HouseholdFinanceMessage"] =
            "Konto domu zostało zamknięte. Historia księgowań została zachowana.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdView)]
    public async Task<IActionResult> Contributions(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await householdFinanceService
                .GetContributionOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        var model =
            new HouseholdContributionIndexViewModel
            {
                Overview =
                    overview
            };

        if (overview is not null &&
            overview.CanManage)
        {
            model.CandidatePeople =
                overview.MemberCandidates
                    .Select(x =>
                        new SelectListItem
                        {
                            Value =
                                x.PersonId.ToString(),
                            Text =
                                $"{x.DisplayName} · {x.LoginName}"
                        })
                    .ToList();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> AddHouseholdMember(
        HouseholdContributionIndexViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!model.CandidatePersonId.HasValue)
        {
            TempData["HouseholdFinanceError"] =
                "Wybierz domownika do dodania.";

            return RedirectToAction(
                nameof(Contributions));
        }

        try
        {
            await householdFinanceService
                .AddHouseholdMemberAsync(
                    new AddHouseholdMemberRequest(
                        model.CandidatePersonId.Value),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);
        }
        catch (ArgumentException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Contributions));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["HouseholdFinanceMessage"] =
            "Domownik został dodany do gospodarstwa.";

        return RedirectToAction(
            nameof(Contributions));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CreateContributionRule(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await householdFinanceService
                .GetContributionOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        if (overview is null)
        {
            TempData["HouseholdFinanceError"] =
                "Najpierw utwórz konto gospodarstwa.";

            return RedirectToAction(
                nameof(Index));
        }

        if (overview.Roles.Count == 0)
        {
            TempData["HouseholdFinanceError"] =
                "Brak roli z aktywnymi użytkownikami, dla której można utworzyć składkę.";

            return RedirectToAction(
                nameof(Contributions));
        }

        if (overview.TargetAccounts.Count == 0)
        {
            TempData["HouseholdFinanceError"] =
                "Brak aktywnego konta docelowego gospodarstwa.";

            return RedirectToAction(
                nameof(Contributions));
        }

        var firstRole =
            overview.Roles[0];

        var firstAccount =
            overview.TargetAccounts[0];

        var model =
            new CreateHouseholdContributionRuleViewModel
            {
                RoleDefinitionId =
                    firstRole.RoleDefinitionId,
                ModeCode =
                    HouseholdContributionModes.FixedAmount,
                FixedAmount =
                    0.01m,
                Percentage =
                    30m,
                DueOffsetDays =
                    7,
                TargetHouseholdAccountId =
                    firstAccount.AccountId,
                ValidFrom =
                    DateTime.Today,
                ReminderDays =
                    3
            };

        RebuildContributionRuleOptions(
            model,
            overview);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CreateContributionRule(
        CreateHouseholdContributionRuleViewModel model,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await householdFinanceService
                .GetContributionOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        if (overview is null)
        {
            return NotFound();
        }

        RebuildContributionRuleOptions(
            model,
            overview);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var result =
                await householdFinanceService
                    .CreateContributionRuleAsync(
                        new CreateHouseholdContributionRuleRequest(
                            model.RoleDefinitionId,
                            model.ModeCode,
                            model.FixedAmount,
                            model.Percentage,
                            model.DueOffsetDays,
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

            TempData["HouseholdFinanceMessage"] =
                result.RulesWaitingForIncomePlan > 0
                    ? $"Reguła została ustawiona dla roli {result.RoleNamePl}. Utworzono {result.RulesCreated} reguł; {result.RulesWaitingForIncomePlan} oczekuje na dodanie planowanego wynagrodzenia."
                    : $"Reguła została ustawiona dla roli {result.RoleNamePl}. Utworzono {result.RulesCreated} indywidualnych reguł dla {result.UsersMatched} użytkowników.";
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

        return RedirectToAction(
            nameof(Contributions));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdView)]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> SendContributionPayment(
        Guid obligationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form =
                await householdFinanceService
                    .GetContributionPaymentFormAsync(
                        obligationId,
                        GetCurrentUserId(),
                        cancellationToken);

            if (form is null)
            {
                TempData["HouseholdFinanceError"] =
                    "Nie znaleziono aktywnego zobowiązania do opłacenia.";

                return RedirectToAction(
                    nameof(Contributions));
            }

            var model =
                BuildContributionPaymentViewModel(
                    form);

            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Contributions));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdView)]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> SendContributionPayment(
        HouseholdContributionPaymentViewModel model,
        CancellationToken cancellationToken = default)
    {
        HouseholdContributionPaymentForm? form = null;

        try
        {
            form =
                await householdFinanceService
                    .GetContributionPaymentFormAsync(
                        model.ObligationId,
                        GetCurrentUserId(),
                        cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Contributions));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            TempData["HouseholdFinanceError"] =
                "Nie znaleziono aktywnego zobowiązania do opłacenia.";

            return RedirectToAction(
                nameof(Contributions));
        }

        RebuildContributionPaymentViewModel(
            model,
            form);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await householdFinanceService
                .SubmitContributionPaymentAsync(
                    new SubmitHouseholdContributionPaymentRequest(
                        model.ObligationId,
                        model.SourcePersonalAccountId,
                        model.Amount),
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
            "Wpłata została wysłana do administratora. Salda zmienią się dopiero po akceptacji.";

        return RedirectToAction(
            nameof(Contributions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdApprove)]
    public async Task<IActionResult> ApproveContributionPayment(
        Guid paymentRequestId,
        string? reviewNote,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await householdFinanceService
                .ApproveContributionPaymentAsync(
                    new ReviewHouseholdContributionPaymentRequest(
                        paymentRequestId,
                        reviewNote),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            TempData["HouseholdFinanceMessage"] =
                "Wpłata została zaakceptowana i zaksięgowana na koncie domu.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Contributions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdApprove)]
    public async Task<IActionResult> RejectContributionPayment(
        Guid paymentRequestId,
        string? reviewNote,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await householdFinanceService
                .RejectContributionPaymentAsync(
                    new ReviewHouseholdContributionPaymentRequest(
                        paymentRequestId,
                        reviewNote),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            TempData["HouseholdFinanceMessage"] =
                "Wpłata została odrzucona. Salda nie zostały zmienione.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Contributions));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdView)]
    public async Task<IActionResult> Invoices(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await householdFinanceService
                .GetInvoiceOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        return View(
            overview);
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public IActionResult CreateInvoice()
    {
        var model =
            new CreateHouseholdInvoiceViewModel
            {
                CategoryCode =
                    HouseholdInvoiceCategories.Other,
                IssueDate =
                    DateTime.Today,
                DueDate =
                    DateTime.Today.AddDays(14),
                GrossAmount =
                    0.01m
            };

        RebuildInvoiceCategoryOptions(
            model);

        return View(
            model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CreateInvoice(
        CreateHouseholdInvoiceViewModel model,
        CancellationToken cancellationToken = default)
    {
        RebuildInvoiceCategoryOptions(
            model);

        if (!ModelState.IsValid)
        {
            return View(
                model);
        }

        try
        {
            await householdFinanceService
                .CreateInvoiceAsync(
                    new CreateHouseholdInvoiceRequest(
                        model.Supplier,
                        model.InvoiceNumber,
                        DateTime.SpecifyKind(
                            model.IssueDate.Date,
                            DateTimeKind.Utc),
                        DateTime.SpecifyKind(
                            model.DueDate.Date,
                            DateTimeKind.Utc),
                        model.GrossAmount,
                        model.CategoryCode,
                        null,
                        model.BillingPeriodFrom.HasValue
                            ? DateTime.SpecifyKind(
                                model.BillingPeriodFrom.Value.Date,
                                DateTimeKind.Utc)
                            : null,
                        model.BillingPeriodTo.HasValue
                            ? DateTime.SpecifyKind(
                                model.BillingPeriodTo.Value.Date,
                                DateTimeKind.Utc)
                            : null,
                        model.MainMeterNumber,
                        model.MainMeterUnit,
                        model.MainMeterPreviousReading,
                        model.MainMeterCurrentReading,
                        model.SubmeterReadingsSnapshot),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return View(
                model);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return View(
                model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["HouseholdFinanceMessage"] =
            "Faktura domu została zapisana. Jej dodanie nie zmieniło jeszcze salda konta domu.";

        return RedirectToAction(
            nameof(Invoices));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> PayInvoice(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form =
                await householdFinanceService
                    .GetInvoicePaymentFormAsync(
                        id,
                        GetCurrentUserId(),
                        cancellationToken);

            if (form is null)
            {
                TempData["HouseholdFinanceError"] =
                    "Nie znaleziono aktywnej faktury z kwotą pozostałą do zapłaty.";

                return RedirectToAction(
                    nameof(Invoices));
            }

            return View(
                BuildInvoicePaymentViewModel(
                    form));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> PayInvoice(
        PayHouseholdInvoiceViewModel model,
        CancellationToken cancellationToken = default)
    {
        HouseholdInvoicePaymentForm? form =
            null;

        try
        {
            form =
                await householdFinanceService
                    .GetInvoicePaymentFormAsync(
                        model.InvoiceId,
                        GetCurrentUserId(),
                        cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            TempData["HouseholdFinanceError"] =
                "Faktura została już opłacona, anulowana albo nie istnieje.";

            return RedirectToAction(
                nameof(Invoices));
        }

        RebuildInvoicePaymentViewModel(
            model,
            form);

        if (!ModelState.IsValid)
        {
            return View(
                model);
        }

        try
        {
            await householdFinanceService
                .PayInvoiceAsync(
                    new PayHouseholdInvoiceRequest(
                        model.CommandId,
                        model.InvoiceId,
                        model.HouseholdAccountId,
                        model.Amount,
                        DateTime.SpecifyKind(
                            model.PaidOn.Date,
                            DateTimeKind.Utc)),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return View(
                model);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return View(
                model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["HouseholdFinanceMessage"] =
            "Płatność faktury została zaksięgowana na wskazanym koncie domu.";

        return RedirectToAction(
            nameof(Invoices));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinanceHouseholdManage)]
    public async Task<IActionResult> CancelInvoice(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await householdFinanceService
                .CancelInvoiceAsync(
                    id,
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            TempData["HouseholdFinanceMessage"] =
                "Faktura została anulowana. Historia rekordu pozostała zachowana.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["HouseholdFinanceError"] =
                exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Invoices));
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
    private static void RebuildTransferOptions(
        HouseholdTransferViewModel model,
        IReadOnlyList<HouseholdAccountSummary> accounts)
    {
        model.SourceAccounts =
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
                            model.SourceAccountId
                    })
                .ToList();

        model.TargetAccounts =
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
                            model.TargetAccountId
                    })
                .ToList();
    }

    private static void RebuildContributionRuleOptions(
        CreateHouseholdContributionRuleViewModel model,
        HouseholdContributionOverview overview)
    {
        model.Roles =
            overview.Roles
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.RoleDefinitionId.ToString(),
                        Text =
                            $"{x.RoleNamePl} · {x.UserCount} użytk.",
                        Selected =
                            x.RoleDefinitionId ==
                            model.RoleDefinitionId
                    })
                .ToList();

        model.Modes =
            HouseholdContributionModes.All
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.Code,
                        Text =
                            x.NamePl,
                        Selected =
                            x.Code ==
                            model.ModeCode
                    })
                .ToList();

        model.TargetAccounts =
            overview.TargetAccounts
                .Where(x =>
                    x.IsActive)
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.AccountId.ToString(),
                        Text =
                            $"{x.Name} · {x.Balance:N2} {x.CurrencyCode}",
                        Selected =
                            x.AccountId ==
                            model.TargetHouseholdAccountId
                    })
                .ToList();
    }

    private static HouseholdContributionPaymentViewModel
        BuildContributionPaymentViewModel(
            HouseholdContributionPaymentForm form)
    {
        var model =
            new HouseholdContributionPaymentViewModel
            {
                ObligationId =
                    form.ObligationId,
                PeriodKey =
                    form.PeriodKey,
                ObligationAmount =
                    form.ObligationAmount,
                PaidAmount =
                    form.PaidAmount,
                OutstandingAmount =
                    form.OutstandingAmount,
                DueDateUtc =
                    form.DueDateUtc,
                CurrencyCode =
                    form.CurrencyCode,
                TargetHouseholdAccountName =
                    form.TargetHouseholdAccountName,
                Amount =
                    form.OutstandingAmount,
                SourcePersonalAccountId =
                    form.SourceAccounts
                        .FirstOrDefault()?
                        .AccountId
                    ?? Guid.Empty
            };

        RebuildContributionPaymentViewModel(
            model,
            form);

        return model;
    }

    private static void RebuildContributionPaymentViewModel(
        HouseholdContributionPaymentViewModel model,
        HouseholdContributionPaymentForm form)
    {
        model.ObligationId =
            form.ObligationId;

        model.PeriodKey =
            form.PeriodKey;

        model.ObligationAmount =
            form.ObligationAmount;

        model.PaidAmount =
            form.PaidAmount;

        model.OutstandingAmount =
            form.OutstandingAmount;

        model.DueDateUtc =
            form.DueDateUtc;

        model.CurrencyCode =
            form.CurrencyCode;

        model.TargetHouseholdAccountName =
            form.TargetHouseholdAccountName;

        model.SourceAccounts =
            form.SourceAccounts
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.AccountId.ToString(),
                        Text =
                            $"{x.Name} · {x.Balance:N2} {x.CurrencyCode}",
                        Selected =
                            x.AccountId ==
                            model.SourcePersonalAccountId
                    })
                .ToList();
    }

    private static void RebuildInvoiceCategoryOptions(
        CreateHouseholdInvoiceViewModel model)
    {
        model.Categories =
            HouseholdInvoiceCategories.All
                .Select(x =>
                    new SelectListItem
                    {
                        Value = x.Code,
                        Text = x.NamePl,
                        Selected =
                            x.Code ==
                            model.CategoryCode
                    })
                .ToList();
    }

    private static PayHouseholdInvoiceViewModel
        BuildInvoicePaymentViewModel(
            HouseholdInvoicePaymentForm form)
    {
        var model =
            new PayHouseholdInvoiceViewModel
            {
                CommandId =
                    Guid.NewGuid(),
                InvoiceId =
                    form.InvoiceId,
                HouseholdAccountId =
                    form.Accounts
                        .FirstOrDefault(x =>
                            x.Balance > 0m)?
                        .AccountId
                    ?? form.Accounts
                        .FirstOrDefault()?
                        .AccountId
                    ?? Guid.Empty,
                Amount =
                    form.RemainingAmount,
                PaidOn =
                    DateTime.Today
            };

        RebuildInvoicePaymentViewModel(
            model,
            form);

        return model;
    }

    private static void RebuildInvoicePaymentViewModel(
        PayHouseholdInvoiceViewModel model,
        HouseholdInvoicePaymentForm form)
    {
        model.InvoiceId =
            form.InvoiceId;

        model.Supplier =
            form.Supplier;

        model.InvoiceNumber =
            form.InvoiceNumber;

        model.DueDateUtc =
            form.DueDateUtc;

        model.GrossAmount =
            form.GrossAmount;

        model.PaidAmount =
            form.PaidAmount;

        model.RemainingAmount =
            form.RemainingAmount;

        model.CategoryNamePl =
            form.CategoryNamePl;

        model.CurrencyCode =
            form.CurrencyCode;

        model.Accounts =
            form.Accounts
                .Select(x =>
                    new SelectListItem
                    {
                        Value =
                            x.AccountId.ToString(),
                        Text =
                            $"{x.Name} · {x.Balance:N2} {x.CurrencyCode}",
                        Selected =
                            x.AccountId ==
                            model.HouseholdAccountId
                    })
                .ToList();
    }

}
