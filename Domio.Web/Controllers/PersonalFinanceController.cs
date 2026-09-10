using System.Security.Claims;
using Domio.Application.PersonalFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Web.Models.PersonalFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class PersonalFinanceController(
    IPersonalFinanceService personalFinanceService) : Controller
{
    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalViewOwn)]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        return View(overview);
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public IActionResult CreateAccount()
    {
        var model =
            new CreatePersonalAccountViewModel
            {
                CurrencyCode = "PLN",
                AccountTypeCode =
                    PersonalAccountTypes.BankAccount
            };

        RebuildAccountTypeOptions(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> CreateAccount(
        CreatePersonalAccountViewModel model,
        CancellationToken cancellationToken = default)
    {
        RebuildAccountTypeOptions(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await personalFinanceService
                .CreateOwnAccountAsync(
                    new CreatePersonalAccountRequest(
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

        TempData["PersonalFinanceMessage"] =
            "Prywatne konto finansowe zostało utworzone.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> AddOperation(
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        var activeAccounts =
            overview.Accounts
                .Where(x => x.IsActive)
                .ToArray();

        if (activeAccounts.Length == 0)
        {
            TempData["PersonalFinanceMessage"] =
                "Najpierw utwórz prywatne konto finansowe.";

            return RedirectToAction(
                nameof(CreateAccount));
        }

        var selectedAccountId =
            accountId.HasValue &&
            activeAccounts.Any(
                x => x.AccountId == accountId.Value)
                ? accountId.Value
                : activeAccounts[0].AccountId;

        var model =
            new AddPersonalOperationViewModel
            {
                AccountId = selectedAccountId,
                KindCode =
                    PersonalTransactionKinds.Expense,
                Amount = 0.01m,
                OccurredOn = DateTime.Today,
                IsRecurring = false,
                FrequencyCode =
                    PersonalRecurringFrequencies.Monthly,
                CategoryCode =
                    PersonalFinanceCategories.OtherExpense
            };

        RebuildOperationOptions(
            model,
            activeAccounts);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> AddOperation(
        AddPersonalOperationViewModel model,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        var activeAccounts =
            overview.Accounts
                .Where(x => x.IsActive)
                .ToArray();

        RebuildOperationOptions(
            model,
            activeAccounts);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            if (model.IsRecurring)
            {
                await personalFinanceService
                    .CreateOwnRecurringRuleAsync(
                        new CreatePersonalRecurringRuleRequest(
                            model.AccountId,
                            model.KindCode,
                            string.IsNullOrWhiteSpace(
                                model.RecurringName)
                                ? model.Description ??
                                    "Operacja cykliczna"
                                : model.RecurringName,
                            model.Amount,
                            model.FrequencyCode,
                            model.CategoryCode,
                            model.Counterparty,
                            DateTime.SpecifyKind(
                                model.OccurredOn.Date,
                                DateTimeKind.Utc),
                            model.RecurringEndOn.HasValue
                                ? DateTime.SpecifyKind(
                                    model.RecurringEndOn.Value.Date,
                                    DateTimeKind.Utc)
                                : null),
                        GetCurrentUserId(),
                        HttpContext.TraceIdentifier,
                        cancellationToken);
            }
            else
            {
                await personalFinanceService
                    .PostOwnOperationAsync(
                        new PostPersonalOperationRequest(
                            model.AccountId,
                            model.KindCode,
                            model.Amount,
                            DateTime.SpecifyKind(
                                model.OccurredOn.Date
                                    .AddHours(12),
                                DateTimeKind.Utc),
                            model.Description,
                            model.CategoryCode,
                            model.Counterparty),
                        GetCurrentUserId(),
                        HttpContext.TraceIdentifier,
                        cancellationToken);
            }
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

        TempData["PersonalFinanceMessage"] =
            model.IsRecurring
                ? "Reguła cykliczna została zapisana. Planowane wystąpienia nie zmieniają salda."
                : model.KindCode ==
                    PersonalTransactionKinds.Income
                    ? "Przychód został zapisany."
                    : "Wydatek został zapisany.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> EditRecurring(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rule =
            await personalFinanceService
                .GetOwnRecurringRuleAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

        if (rule is null)
        {
            return NotFound();
        }

        if (!rule.IsActive)
        {
            TempData["PersonalFinanceMessage"] =
                "Ta reguła cykliczna została zakończona.";

            return RedirectToAction(nameof(Index));
        }

        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        var model =
            new EditPersonalRecurringRuleViewModel
            {
                RuleId = rule.RuleId,
                AccountId = rule.AccountId,
                KindCode = rule.KindCode,
                Name = rule.Name,
                PlannedAmount = rule.PlannedAmount,
                FrequencyCode = rule.FrequencyCode,
                CategoryCode = rule.CategoryCode,
                Counterparty = rule.Counterparty,
                StartDate =
                    rule.StartDateUtc.ToLocalTime().Date,
                EndDate =
                    rule.EndDateUtc?.ToLocalTime().Date
            };

        RebuildRecurringRuleOptions(
            model,
            overview.Accounts
                .Where(x => x.IsActive)
                .ToArray());

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> EditRecurring(
        EditPersonalRecurringRuleViewModel model,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        RebuildRecurringRuleOptions(
            model,
            overview.Accounts
                .Where(x => x.IsActive)
                .ToArray());

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await personalFinanceService
                .UpdateOwnRecurringRuleAsync(
                    new UpdatePersonalRecurringRuleRequest(
                        model.RuleId,
                        model.AccountId,
                        model.KindCode,
                        model.Name,
                        model.PlannedAmount,
                        model.FrequencyCode,
                        model.CategoryCode,
                        model.Counterparty,
                        DateTime.SpecifyKind(
                            model.StartDate.Date,
                            DateTimeKind.Utc),
                        model.EndDate.HasValue
                            ? (DateTime?)DateTime.SpecifyKind(
                                model.EndDate.Value.Date,
                                DateTimeKind.Utc)
                            : null),
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

        TempData["PersonalFinanceMessage"] =
            "Reguła cykliczna została zaktualizowana. Zmiany dotyczą przyszłego planu.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> DeleteRecurring(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var rule =
            await personalFinanceService
                .GetOwnRecurringRuleAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

        if (rule is null)
        {
            return NotFound();
        }

        return View(
            new DeletePersonalRecurringRuleViewModel
            {
                RuleId = rule.RuleId,
                Name = rule.Name,
                AccountName = rule.AccountName,
                KindNamePl = rule.KindNamePl,
                PlannedAmount = rule.PlannedAmount,
                CurrencyCode = rule.CurrencyCode,
                FrequencyNamePl = rule.FrequencyNamePl
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> DeleteRecurringConfirmed(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await personalFinanceService
                .DeactivateOwnRecurringRuleAsync(
                    ruleId,
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["PersonalFinanceMessage"] =
            "Reguła cykliczna została zakończona. Przyszłe planowane wystąpienia usunięto, a historia pozostała zachowana.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> ConfirmRecurring(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var occurrence =
            await personalFinanceService
                .GetOwnRecurringOccurrenceAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

        if (occurrence is null)
        {
            return NotFound();
        }

        if (occurrence.StatusCode !=
            PersonalRecurringOccurrenceStatuses.Planned)
        {
            TempData["PersonalFinanceMessage"] =
                "To wystąpienie zostało już rozliczone.";

            return RedirectToAction(nameof(Index));
        }

        return View(
            new ConfirmRecurringOccurrenceViewModel
            {
                OccurrenceId =
                    occurrence.OccurrenceId,
                RuleName =
                    occurrence.RuleName,
                AccountName =
                    occurrence.AccountName,
                KindCode =
                    occurrence.KindCode,
                KindNamePl =
                    occurrence.KindNamePl,
                CurrencyCode =
                    occurrence.CurrencyCode,
                PlannedAmount =
                    occurrence.PlannedAmount,
                PlannedDate =
                    occurrence.PlannedDateUtc
                        .ToLocalTime()
                        .Date,
                ActualAmount =
                    occurrence.PlannedAmount,
                ActualDate =
                    occurrence.PlannedDateUtc
                        .ToLocalTime()
                        .Date,
                Description =
                    occurrence.RuleName
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> ConfirmRecurring(
        ConfirmRecurringOccurrenceViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await personalFinanceService
                .ConfirmOwnRecurringOccurrenceAsync(
                    new ConfirmPersonalRecurringOccurrenceRequest(
                        model.OccurrenceId,
                        model.ActualAmount,
                        DateTime.SpecifyKind(
                            model.ActualDate.Date
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

        TempData["PersonalFinanceMessage"] =
            model.KindCode ==
                PersonalTransactionKinds.Income
                ? "Wpływ został potwierdzony i dodany do salda konta."
                : "Płatność została potwierdzona i odjęta od salda konta.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> Transfer(
        Guid? sourceAccountId,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        var activeAccounts =
            overview.Accounts
                .Where(x => x.IsActive)
                .ToArray();

        if (activeAccounts.Length < 2)
        {
            TempData["PersonalFinanceMessage"] =
                "Aby wykonać transfer, potrzebujesz co najmniej dwóch aktywnych prywatnych kont.";

            return RedirectToAction(nameof(Index));
        }

        var selectedSourceId =
            sourceAccountId.HasValue &&
            activeAccounts.Any(x =>
                x.AccountId == sourceAccountId.Value)
                ? sourceAccountId.Value
                : activeAccounts[0].AccountId;

        var selectedTargetId =
            activeAccounts
                .First(x =>
                    x.AccountId != selectedSourceId)
                .AccountId;

        var model =
            new PersonalTransferViewModel
            {
                SourceAccountId =
                    selectedSourceId,
                TargetAccountId =
                    selectedTargetId,
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
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> Transfer(
        PersonalTransferViewModel model,
        CancellationToken cancellationToken = default)
    {
        var overview =
            await personalFinanceService
                .GetOwnOverviewAsync(
                    GetCurrentUserId(),
                    cancellationToken);

        var activeAccounts =
            overview.Accounts
                .Where(x => x.IsActive)
                .ToArray();

        RebuildTransferOptions(
            model,
            activeAccounts);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await personalFinanceService
                .TransferBetweenOwnAccountsAsync(
                    new CreatePersonalTransferRequest(
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

        TempData["PersonalFinanceMessage"] =
            "Transfer pomiędzy własnymi kontami został wykonany.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> CloseAccount(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var info =
            await personalFinanceService
                .GetOwnAccountClosureInfoAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

        if (info is null)
        {
            return NotFound();
        }

        return View(
            new ClosePersonalAccountViewModel
            {
                AccountId = info.AccountId,
                Name = info.Name,
                AccountTypeNamePl =
                    info.AccountTypeNamePl,
                CurrencyCode =
                    info.CurrencyCode,
                Balance =
                    info.Balance,
                IsActive =
                    info.IsActive,
                ActiveRecurringRules =
                    info.ActiveRecurringRules,
                PlannedRecurringOccurrences =
                    info.PlannedRecurringOccurrences
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> CloseAccountConfirmed(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await personalFinanceService
                .CloseOwnAccountAsync(
                    accountId,
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["PersonalFinanceError"] =
                exception.Message;

            return RedirectToAction(
                nameof(CloseAccount),
                new { id = accountId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        TempData["PersonalFinanceMessage"] =
            "Konto zostało zamknięte. Historia operacji pozostała zachowana.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> CorrectOperation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var info =
            await personalFinanceService
                .GetOwnTransactionCorrectionInfoAsync(
                    id,
                    GetCurrentUserId(),
                    cancellationToken);

        if (info is null)
        {
            return NotFound();
        }

        if (!info.CanCorrect)
        {
            TempData["PersonalFinanceError"] =
                info.AlreadyCorrected
                    ? "Ta operacja ma już zapisaną korektę."
                    : "Tej operacji nie można skorygować.";

            return RedirectToAction(nameof(Index));
        }

        var model =
            new CorrectPersonalTransactionViewModel
            {
                TransactionId =
                    info.TransactionId,
                AccountName =
                    info.AccountName,
                CurrencyCode =
                    info.CurrencyCode,
                KindNamePl =
                    info.KindNamePl,
                OriginalAmount =
                    info.OriginalAmount,
                CorrectedAmount =
                    info.OriginalAmount,
                OccurredAt =
                    info.OccurredAtUtc
                        .ToLocalTime(),
                OriginalDescription =
                    info.Description,
                CategoryCode =
                    info.CategoryCode
            };

        RebuildCorrectionCategories(
            model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(
        Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> CorrectOperation(
        CorrectPersonalTransactionViewModel model,
        CancellationToken cancellationToken = default)
    {
        var info =
            await personalFinanceService
                .GetOwnTransactionCorrectionInfoAsync(
                    model.TransactionId,
                    GetCurrentUserId(),
                    cancellationToken);

        if (info is null)
        {
            return NotFound();
        }

        model.AccountName =
            info.AccountName;
        model.CurrencyCode =
            info.CurrencyCode;
        model.KindNamePl =
            info.KindNamePl;
        model.OriginalAmount =
            info.OriginalAmount;
        model.OccurredAt =
            info.OccurredAtUtc
                .ToLocalTime();
        model.OriginalDescription =
            info.Description;

        RebuildCorrectionCategories(
            model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await personalFinanceService
                .CorrectOwnTransactionAsync(
                    new CorrectPersonalTransactionRequest(
                        model.TransactionId,
                        model.CorrectedAmount,
                        model.CategoryCode),
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

        TempData["PersonalFinanceMessage"] =
            "Korekta została zapisana. Operacja źródłowa pozostała w historii.";

        return RedirectToAction(nameof(Index));
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
        CreatePersonalAccountViewModel model)
    {
        model.AccountTypes =
            PersonalAccountTypes.All
                .Select(x =>
                    new SelectListItem
                    {
                        Value = x.Code,
                        Text = x.NamePl,
                        Selected =
                            string.Equals(
                                x.Code,
                                model.AccountTypeCode,
                                StringComparison.Ordinal)
                    })
                .ToList();
    }

    private static void RebuildOperationOptions(
        AddPersonalOperationViewModel model,
        IReadOnlyList<PersonalAccountSummary> accounts)
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

        model.OperationKinds =
        [
            new SelectListItem
            {
                Value =
                    PersonalTransactionKinds.Expense,
                Text = "Wydatek",
                Selected =
                    model.KindCode ==
                    PersonalTransactionKinds.Expense
            },
            new SelectListItem
            {
                Value =
                    PersonalTransactionKinds.Income,
                Text = "Przychód",
                Selected =
                    model.KindCode ==
                    PersonalTransactionKinds.Income
            }
        ];

        model.RecurringFrequencies =
            PersonalRecurringFrequencies.All
                .Select(x =>
                    new SelectListItem
                    {
                        Value = x.Code,
                        Text = x.NamePl,
                        Selected =
                            x.Code ==
                            model.FrequencyCode
                    })
                .ToList();

        model.Categories =
            PersonalFinanceCategories.All
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
    private static void RebuildRecurringRuleOptions(
        EditPersonalRecurringRuleViewModel model,
        IReadOnlyList<PersonalAccountSummary> accounts)
    {
        model.Accounts =
            accounts
                .Select(x =>
                    new SelectListItem
                    {
                        Value = x.AccountId.ToString(),
                        Text =
                            $"{x.Name} · {x.Balance:N2} {x.CurrencyCode}",
                        Selected =
                            x.AccountId ==
                            model.AccountId
                    })
                .ToList();

        model.OperationKinds =
        [
            new SelectListItem
            {
                Value =
                    PersonalTransactionKinds.Expense,
                Text = "Wydatek",
                Selected =
                    model.KindCode ==
                    PersonalTransactionKinds.Expense
            },
            new SelectListItem
            {
                Value =
                    PersonalTransactionKinds.Income,
                Text = "Przychód",
                Selected =
                    model.KindCode ==
                    PersonalTransactionKinds.Income
            }
        ];

        model.RecurringFrequencies =
            PersonalRecurringFrequencies.All
                .Select(x =>
                    new SelectListItem
                    {
                        Value = x.Code,
                        Text = x.NamePl,
                        Selected =
                            x.Code ==
                            model.FrequencyCode
                    })
                .ToList();

        model.Categories =
            PersonalFinanceCategories.All
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

    private static void RebuildTransferOptions(
        PersonalTransferViewModel model,
        IReadOnlyList<PersonalAccountSummary> accounts)
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

    private static void RebuildCorrectionCategories(
        CorrectPersonalTransactionViewModel model)
    {
        model.Categories =
            PersonalFinanceCategories.All
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

}
