using System.Security.Claims;
using Domio.Application.FamilyFinance;
using Domio.Domain.FamilyFinance;
using Domio.Domain.PersonalFinance;
using Domio.Domain.Users;
using Domio.Web.Models.FamilyFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Domio.Web.Controllers;

[Authorize]
public sealed class FamilyFinanceController(
    IFamilyFinanceService familyFinanceService,
    IFamilyBudgetService familyBudgetService,
    IFamilyAreaService familyAreaService) : Controller
{
    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> Index(
        Guid? familyGroupId,
        int? year,
        int? month,
        string? tab,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;

        try
        {
            var overview =
                await familyFinanceService.GetOverviewAsync(
                    GetCurrentUserId(),
                    familyGroupId,
                    selectedYear,
                    selectedMonth,
                    cancellationToken);

            FamilyBudgetOverview? budget = null;

            if (overview.SelectedFamilyGroupId.HasValue)
            {
                budget = await familyBudgetService.GetOverviewAsync(
                    overview.SelectedFamilyGroupId.Value,
                    GetCurrentUserId(),
                    selectedYear,
                    selectedMonth,
                    cancellationToken);

                await familyAreaService.NormalizeScheduledOccurrencesAsync(
                    overview.SelectedFamilyGroupId.Value,
                    GetCurrentUserId(),
                    selectedYear,
                    selectedMonth,
                    cancellationToken);

                // Ponowne odczytanie odświeża datę terminu w modelu budżetu.
                budget = await familyBudgetService.GetOverviewAsync(
                    overview.SelectedFamilyGroupId.Value,
                    GetCurrentUserId(),
                    selectedYear,
                    selectedMonth,
                    cancellationToken);
            }

            return View(
                new FamilyFinanceIndexViewModel
                {
                    Overview = overview,
                    Budget = budget,
                    SelectedTab = NormalizeFamilyFinanceTab(tab)
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public IActionResult CreateGroup()
    {
        return View(
            new CreateFamilyGroupViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> CreateGroup(
        CreateFamilyGroupViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var groupId =
                await familyFinanceService.CreateGroupAsync(
                    new CreateFamilyGroupRequest(
                        model.Name),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Grupa rodzinna została utworzona. Domyślnie żadne prywatne dane finansowe nie są udostępniane.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = groupId
                });
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
    }


    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> CreateSharedAccount(
        Guid familyGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form = await familyFinanceService
                .GetCreateSharedAccountFormAsync(
                    familyGroupId,
                    GetCurrentUserId(),
                    cancellationToken);

            if (form is null)
            {
                return NotFound();
            }

            var model = new CreateFamilySharedAccountViewModel
            {
                FamilyGroupId = form.FamilyGroupId,
                FamilyGroupName = form.FamilyGroupName,
                AccountTypeCode = PersonalAccountTypes.BankAccount
            };

            RebuildSharedAccountOptions(model, form);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> CreateSharedAccount(
        CreateFamilySharedAccountViewModel model,
        CancellationToken cancellationToken = default)
    {
        CreateFamilySharedAccountForm? form;

        try
        {
            form = await familyFinanceService
                .GetCreateSharedAccountFormAsync(
                    model.FamilyGroupId,
                    GetCurrentUserId(),
                    cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            return NotFound();
        }

        RebuildSharedAccountOptions(model, form);

        if (model.OwnerPersonId != Guid.Empty &&
            model.OwnerPersonId == model.CoOwnerPersonId)
        {
            ModelState.AddModelError(
                nameof(model.CoOwnerPersonId),
                "Współwłaściciel musi być inną osobą niż właściciel.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyFinanceService.CreateSharedAccountAsync(
                new CreateFamilySharedAccountRequest(
                    model.FamilyGroupId,
                    model.Name,
                    model.AccountTypeCode,
                    model.InitialBalance,
                    model.OwnerPersonId,
                    model.CoOwnerPersonId),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Wspólne konto rodziny zostało utworzone. Właściciel i współwłaściciel zobaczą je również w Finansach osobistych.";

            return RedirectToAction(
                nameof(Index),
                new { familyGroupId = model.FamilyGroupId });
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

    [HttpGet]
    [Authorize(Policy = SystemPermissions.FinancePersonalViewOwn)]
    public async Task<IActionResult> SharedAccountOperation(
        Guid sharedAccountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form = await familyFinanceService
                .GetSharedAccountOperationFormAsync(
                    sharedAccountId,
                    GetCurrentUserId(),
                    cancellationToken);

            if (form is null)
            {
                return NotFound();
            }

            var model = new FamilySharedAccountOperationViewModel
            {
                SharedAccountId = form.SharedAccountId,
                FamilyGroupId = form.FamilyGroupId,
                FamilyGroupName = form.FamilyGroupName,
                AccountName = form.AccountName,
                CurrencyCode = form.CurrencyCode,
                Balance = form.Balance,
                CurrentPersonRoleNamePl = form.CurrentPersonRoleNamePl,
                KindCode = PersonalTransactionKinds.Expense,
                CategoryCode = PersonalFinanceCategories.OtherExpense,
                OccurredAtUtc = DateTime.Today
            };

            RebuildSharedAccountOperationOptions(model);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.FinancePersonalManageOwn)]
    public async Task<IActionResult> SharedAccountOperation(
        FamilySharedAccountOperationViewModel model,
        CancellationToken cancellationToken = default)
    {
        FamilySharedAccountOperationForm? form;

        try
        {
            form = await familyFinanceService
                .GetSharedAccountOperationFormAsync(
                    model.SharedAccountId,
                    GetCurrentUserId(),
                    cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            return NotFound();
        }

        model.FamilyGroupId = form.FamilyGroupId;
        model.FamilyGroupName = form.FamilyGroupName;
        model.AccountName = form.AccountName;
        model.CurrencyCode = form.CurrencyCode;
        model.Balance = form.Balance;
        model.CurrentPersonRoleNamePl = form.CurrentPersonRoleNamePl;
        RebuildSharedAccountOperationOptions(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyFinanceService.PostSharedAccountOperationAsync(
                new PostFamilySharedAccountOperationRequest(
                    model.SharedAccountId,
                    model.KindCode,
                    model.Amount,
                    model.OccurredAtUtc,
                    model.Description,
                    model.CategoryCode,
                    model.Counterparty),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["PersonalFinanceMessage"] =
                "Operacja na wspólnym koncie rodziny została zaksięgowana.";

            return RedirectToAction(
                "Index",
                "PersonalFinance");
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

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> AddMember(
        Guid familyGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form =
                await familyFinanceService.GetAddMemberFormAsync(
                    familyGroupId,
                    GetCurrentUserId(),
                    cancellationToken);

            if (form is null)
            {
                return NotFound();
            }

            var model =
                new AddFamilyMemberViewModel
                {
                    FamilyGroupId = form.FamilyGroupId,
                    FamilyGroupName = form.FamilyGroupName,
                    FamilyRoleCode = FamilyRoles.Adult
                };

            RebuildMemberOptions(
                model,
                form.Candidates);

            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> AddMember(
        AddFamilyMemberViewModel model,
        CancellationToken cancellationToken = default)
    {
        AddFamilyMemberForm? form = null;

        try
        {
            form =
                await familyFinanceService.GetAddMemberFormAsync(
                    model.FamilyGroupId,
                    GetCurrentUserId(),
                    cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            return NotFound();
        }

        model.FamilyGroupName = form.FamilyGroupName;
        RebuildMemberOptions(
            model,
            form.Candidates);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyFinanceService.AddMemberAsync(
                new AddFamilyMemberRequest(
                    model.FamilyGroupId,
                    model.PersonId,
                    model.FamilyRoleCode),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Członek został dodany do rodziny.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId
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
    public async Task<IActionResult> EndMember(
        Guid membershipId,
        Guid familyGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await familyFinanceService.EndMembershipAsync(
                membershipId,
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Członkostwo zostało zakończone. Historia rodziny pozostaje zachowana.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index),
            new
            {
                familyGroupId
            });
    }

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.ShareOwn)]
    public async Task<IActionResult> Sharing(
        Guid familyGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sharing =
                await familyFinanceService.GetOwnSharingAsync(
                    familyGroupId,
                    GetCurrentUserId(),
                    cancellationToken);

            if (sharing is null)
            {
                return NotFound();
            }

            return View(
                new FamilySharingViewModel
                {
                    FamilyGroupId = sharing.FamilyGroupId,
                    FamilyGroupName = sharing.FamilyGroupName,
                    PersonDisplayName = sharing.PersonDisplayName,
                    SharePlannedIncome = sharing.SharePlannedIncome,
                    ShareActualIncome = sharing.ShareActualIncome,
                    ShareFamilyExpenses = sharing.ShareFamilyExpenses,
                    ShareRecurringRules = sharing.ShareRecurringRules
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.ShareOwn)]
    public async Task<IActionResult> Sharing(
        FamilySharingViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyFinanceService.UpdateOwnSharingAsync(
                new UpdateFamilySharingRequest(
                    model.FamilyGroupId,
                    model.SharePlannedIncome,
                    model.ShareActualIncome,
                    model.ShareFamilyExpenses,
                    model.ShareRecurringRules),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Zakres udostępniania został zapisany. Zmiana działa od teraz i nie usuwa historii audytu.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId
                });
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
    public async Task<IActionResult> CreateChildIncome(
        Guid familyGroupId,
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var overview = await familyFinanceService.GetOverviewAsync(
                GetCurrentUserId(),
                familyGroupId,
                today.Year,
                today.Month,
                cancellationToken);

            var child = overview.Members.SingleOrDefault(x =>
                x.PersonId == personId &&
                x.FamilyRoleCode == FamilyRoles.Child);

            if (child is null || overview.SelectedFamilyGroupId != familyGroupId)
            {
                return NotFound();
            }

            var model = new CreateFamilyChildIncomeViewModel
            {
                FamilyGroupId = familyGroupId,
                BeneficiaryPersonId = personId,
                FamilyGroupName = overview.SelectedFamilyGroupName ?? string.Empty,
                BeneficiaryDisplayName = child.DisplayName,
                IncomeKindCode = FamilyIncomeKinds.ChildBenefit800Plus,
                PlannedAmount = FamilyIncomeKinds.GetSuggestedAmount(
                    FamilyIncomeKinds.ChildBenefit800Plus) ?? 800m,
                FrequencyCode = FamilyRecurringFrequencies.Monthly,
                DueDay = Math.Min(today.Day, 28),
                ActiveFromUtc = today
            };

            RebuildChildIncomeOptions(model);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> CreateChildIncome(
        CreateFamilyChildIncomeViewModel model,
        CancellationToken cancellationToken = default)
    {
        var period = model.ActiveFromUtc == default
            ? DateTime.Today
            : model.ActiveFromUtc;

        try
        {
            var overview = await familyFinanceService.GetOverviewAsync(
                GetCurrentUserId(),
                model.FamilyGroupId,
                period.Year,
                period.Month,
                cancellationToken);

            var child = overview.Members.SingleOrDefault(x =>
                x.PersonId == model.BeneficiaryPersonId &&
                x.FamilyRoleCode == FamilyRoles.Child);

            if (child is null)
            {
                return NotFound();
            }

            model.FamilyGroupName = overview.SelectedFamilyGroupName ?? string.Empty;
            model.BeneficiaryDisplayName = child.DisplayName;
            RebuildChildIncomeOptions(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await familyFinanceService.CreateChildIncomeAsync(
                new CreateFamilyChildIncomeRequest(
                    model.FamilyGroupId,
                    model.BeneficiaryPersonId,
                    model.IncomeKindCode,
                    model.CustomName,
                    model.PlannedAmount,
                    model.FrequencyCode,
                    model.DueDay,
                    model.ActiveFromUtc,
                    model.ActiveToUtc),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                $"Dodano planowany przychód dla {child.DisplayName}. Nie utworzono salda ani konta dziecka.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = period.Year,
                    month = period.Month
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

        RebuildChildIncomeOptions(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> DeactivateChildIncome(
        Guid ruleId,
        Guid familyGroupId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await familyFinanceService.DeactivateChildIncomeAsync(
                ruleId,
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Przychód dziecka został zakończony. Wcześniejszy plan pozostaje w historii.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index),
            new { familyGroupId, year, month });
    }

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> ConfirmChildIncome(
        Guid familyGroupId,
        Guid ruleId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form = await familyFinanceService.GetChildIncomeReceiptFormAsync(
                familyGroupId,
                ruleId,
                year,
                month,
                GetCurrentUserId(),
                cancellationToken);

            if (form is null)
            {
                return NotFound();
            }

            return View(BuildConfirmChildIncomeViewModel(form));
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
            return RedirectToAction(
                nameof(Index),
                new { familyGroupId, year, month });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> ConfirmChildIncome(
        ConfirmFamilyChildIncomeReceiptViewModel model,
        CancellationToken cancellationToken = default)
    {
        FamilyChildIncomeReceiptForm? form;

        try
        {
            form = await familyFinanceService.GetChildIncomeReceiptFormAsync(
                model.FamilyGroupId,
                model.RuleId,
                model.Year,
                model.Month,
                GetCurrentUserId(),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = model.Year,
                    month = model.Month,
                    tab = "children"
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            return NotFound();
        }

        RebuildConfirmChildIncomeViewModel(model, form);

        if (!TryParseIncomeReceiptAccountKey(
                model.SelectedAccountKey,
                out var accountType,
                out var accountId))
        {
            ModelState.AddModelError(
                nameof(model.SelectedAccountKey),
                "Wybierz poprawne konto dla wpływu.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyFinanceService.ConfirmChildIncomeReceiptAsync(
                new ConfirmFamilyChildIncomeReceiptRequest(
                    model.FamilyGroupId,
                    model.RuleId,
                    model.Year,
                    model.Month,
                    accountType!,
                    accountId,
                    model.ReceivedAtUtc),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                $"Potwierdzono wpływ {form.RuleName} dla {form.BeneficiaryDisplayName}. Kwota została zaksięgowana na wybranym koncie.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = model.Year,
                    month = model.Month
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

        return View(model);
    }


    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> PayChildContribution(
        Guid familyGroupId,
        Guid obligationId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form =
                await familyFinanceService
                    .GetChildContributionPaymentFormAsync(
                        familyGroupId,
                        obligationId,
                        GetCurrentUserId(),
                        cancellationToken);

            if (form is null)
            {
                return NotFound();
            }

            return View(
                BuildPayChildContributionViewModel(
                    form,
                    year,
                    month));
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId,
                    year,
                    month,
                    tab = "child-contributions"
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
    public async Task<IActionResult> PayChildContribution(
        PayFamilyChildContributionViewModel model,
        CancellationToken cancellationToken = default)
    {
        FamilyChildContributionPaymentForm? form;

        try
        {
            form =
                await familyFinanceService
                    .GetChildContributionPaymentFormAsync(
                        model.FamilyGroupId,
                        model.ObligationId,
                        GetCurrentUserId(),
                        cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] =
                exception.Message;

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = model.Year,
                    month = model.Month,
                    tab = "child-contributions"
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            return NotFound();
        }

        RebuildPayChildContributionViewModel(
            model,
            form);

        if (!TryParseChildContributionSourceKey(
                model.SelectedSourceKey,
                out var sourceType,
                out var sourceId))
        {
            ModelState.AddModelError(
                nameof(model.SelectedSourceKey),
                "Wybierz poprawne źródło środków.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyFinanceService
                .PayChildContributionAsync(
                    new PayFamilyChildContributionRequest(
                        model.FamilyGroupId,
                        model.ObligationId,
                        sourceType!,
                        sourceId,
                        model.PaidAtUtc),
                    GetCurrentUserId(),
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            TempData["FamilyFinanceMessage"] =
                $"Przekazano składkę za {form.ChildDisplayName} do budżetu domu.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = model.Year,
                    month = model.Month,
                    tab = "child-contributions"
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
    public async Task<IActionResult> CreateExpense(
        Guid familyGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            var form = await familyBudgetService.GetLinkFormAsync(
                familyGroupId,
                GetCurrentUserId(),
                today.Year,
                today.Month,
                cancellationToken);

            var model = new CreateFamilyExpenseViewModel
            {
                FamilyGroupId = familyGroupId,
                FamilyGroupName = form.FamilyGroupName,
                CategoryCode = FamilyBudgetCategories.Other,
                FrequencyCode = FamilyRecurringFrequencies.Monthly,
                DueDay = Math.Min(today.Day, 28),
                ActiveFromUtc = today
            };

            RebuildExpenseOptions(model, form.Beneficiaries);
            return View(model);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> CreateExpense(
        CreateFamilyExpenseViewModel model,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var period = model.ActiveFromUtc == default
                ? DateTime.Today
                : model.ActiveFromUtc;
            var form = await familyBudgetService.GetLinkFormAsync(
                model.FamilyGroupId,
                GetCurrentUserId(),
                period.Year,
                period.Month,
                cancellationToken);

            model.FamilyGroupName = form.FamilyGroupName;
            RebuildExpenseOptions(model, form.Beneficiaries);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            await familyBudgetService.CreateRecurringExpenseAsync(
                new CreateFamilyRecurringExpenseRequest(
                    model.FamilyGroupId,
                    model.Name,
                    model.CategoryCode,
                    model.PlannedAmount,
                    model.FrequencyCode,
                    model.DueDay,
                    model.BeneficiaryPersonId,
                    model.ActiveFromUtc,
                    model.ActiveToUtc),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Planowany koszt został dodany. Nie zmieniono salda żadnego konta.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = period.Year,
                    month = period.Month
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

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.Manage)]
    public async Task<IActionResult> DeactivateExpense(
        Guid ruleId,
        Guid familyGroupId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await familyBudgetService.DeactivateRecurringExpenseAsync(
                ruleId,
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Reguła kosztu została zakończona. Historyczne plany pozostały zachowane.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(Index),
            new { familyGroupId, year, month });
    }

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> PayExpense(
        Guid familyGroupId,
        Guid occurrenceId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var form = await familyBudgetService.GetExpensePaymentFormAsync(
                familyGroupId,
                occurrenceId,
                GetCurrentUserId(),
                cancellationToken);

            if (form is null)
            {
                return NotFound();
            }

            return View(BuildPayExpenseViewModel(form, year, month));
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
            return RedirectToAction(
                nameof(Index),
                new { familyGroupId, year, month });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> PayExpense(
        PayFamilyExpenseViewModel model,
        CancellationToken cancellationToken = default)
    {
        FamilyExpensePaymentForm? form;

        try
        {
            form = await familyBudgetService.GetExpensePaymentFormAsync(
                model.FamilyGroupId,
                model.OccurrenceId,
                GetCurrentUserId(),
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = model.Year,
                    month = model.Month
                });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        if (form is null)
        {
            return NotFound();
        }

        RebuildPayExpenseViewModel(model, form);

        if (!TryParsePaymentAccountKey(
                model.SelectedAccountKey,
                out var paymentAccountType,
                out var accountId))
        {
            ModelState.AddModelError(
                nameof(model.SelectedAccountKey),
                "Wybierz poprawne konto do płatności.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await familyBudgetService.PayExpenseAsync(
                new PayFamilyExpenseRequest(
                    model.FamilyGroupId,
                    model.OccurrenceId,
                    paymentAccountType!,
                    accountId,
                    model.PaidAtUtc),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Koszt został opłacony i zaksięgowany na wybranym koncie. Wykonanie budżetu rodzinnego zostało zaktualizowane.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    familyGroupId = model.FamilyGroupId,
                    year = model.Year,
                    month = model.Month
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

        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> BudgetLinks(
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
            var form = await familyBudgetService.GetLinkFormAsync(
                familyGroupId,
                GetCurrentUserId(),
                selectedYear,
                selectedMonth,
                cancellationToken);

            return View(BuildBudgetLinksViewModel(form));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
            return RedirectToAction(nameof(Index), new { familyGroupId });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> LinkBudgetSource(
        Guid familyGroupId,
        int year,
        int month,
        string selectedSourceKey,
        string categoryCode,
        Guid? beneficiaryPersonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parts = (selectedSourceKey ?? string.Empty).Split('|', 2);
            if (parts.Length != 2 ||
                !FamilyBudgetSourceTypes.IsValid(parts[0]) ||
                !Guid.TryParse(parts[1], out var sourceId))
            {
                throw new ArgumentException("Wybierz poprawne źródło.");
            }

            await familyBudgetService.LinkSourceAsync(
                new CreateFamilyBudgetLinkRequest(
                    familyGroupId,
                    parts[0],
                    sourceId,
                    categoryCode,
                    beneficiaryPersonId),
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Źródło zostało powiązane z budżetem rodzinnym. Nie utworzono drugiej operacji finansowej.";
        }
        catch (ArgumentException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(BudgetLinks),
            new { familyGroupId, year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = FamilyFinancePermissions.View)]
    public async Task<IActionResult> UnlinkBudgetSource(
        Guid linkId,
        Guid familyGroupId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await familyBudgetService.UnlinkSourceAsync(
                linkId,
                GetCurrentUserId(),
                HttpContext.TraceIdentifier,
                cancellationToken);

            TempData["FamilyFinanceMessage"] =
                "Źródło zostało odłączone od budżetu rodzinnego.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["FamilyFinanceError"] = exception.Message;
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }

        return RedirectToAction(
            nameof(BudgetLinks),
            new { familyGroupId, year, month });
    }

    private static void RebuildChildIncomeOptions(
        CreateFamilyChildIncomeViewModel model)
    {
        model.IncomeKinds = FamilyIncomeKinds.All
            .Select(x =>
            {
                var suggested = FamilyIncomeKinds.GetSuggestedAmount(x.Code);
                var label = suggested.HasValue
                    ? $"{x.NamePl} ({suggested.Value:N2} zł)"
                    : x.NamePl;

                return new SelectListItem(label, x.Code);
            })
            .ToList();

        model.Frequencies = FamilyRecurringFrequencies.All
            .Select(x => new SelectListItem(x.NamePl, x.Code))
            .ToList();
    }

    private static void RebuildExpenseOptions(
        CreateFamilyExpenseViewModel model,
        IReadOnlyList<FamilyBudgetBeneficiary> beneficiaries)
    {
        model.Categories = FamilyBudgetCategories.All
            .Select(x => new SelectListItem(x.NamePl, x.Code))
            .ToList();

        model.Frequencies = FamilyRecurringFrequencies.All
            .Select(x => new SelectListItem(x.NamePl, x.Code))
            .ToList();

        model.Beneficiaries =
        [
            new SelectListItem("— koszt wspólny rodziny —", string.Empty),
            .. beneficiaries.Select(x =>
                new SelectListItem(
                    $"{x.DisplayName} ({FamilyRoles.GetNamePl(x.FamilyRoleCode)})",
                    x.PersonId.ToString()))
        ];
    }

    private static FamilyBudgetLinksViewModel BuildBudgetLinksViewModel(
        FamilyBudgetLinkForm form)
    {
        return new FamilyBudgetLinksViewModel
        {
            Form = form,
            Sources = form.Candidates
                .Select(x =>
                    new SelectListItem(
                        $"{x.SourceTypeNamePl}: {x.DisplayName} · {x.PlannedOrActualAmount:N2} {x.CurrencyCode}",
                        $"{x.SourceType}|{x.SourceId:D}"))
                .ToList(),
            Categories = FamilyBudgetCategories.All
                .Select(x => new SelectListItem(x.NamePl, x.Code))
                .ToList(),
            Beneficiaries =
            [
                new SelectListItem("— bez przypisania do osoby —", string.Empty),
                .. form.Beneficiaries.Select(x =>
                    new SelectListItem(
                        $"{x.DisplayName} ({FamilyRoles.GetNamePl(x.FamilyRoleCode)})",
                        x.PersonId.ToString()))
            ]
        };
    }


    private static void RebuildSharedAccountOptions(
        CreateFamilySharedAccountViewModel model,
        CreateFamilySharedAccountForm form)
    {
        model.FamilyGroupName = form.FamilyGroupName;
        model.AccountTypes = PersonalAccountTypes.All
            .Select(x =>
                new SelectListItem(
                    x.NamePl,
                    x.Code))
            .ToList();
        model.Adults = form.AdultMembers
            .Select(x =>
                new SelectListItem(
                    x.DisplayName,
                    x.PersonId.ToString()))
            .ToList();
    }

    private static void RebuildSharedAccountOperationOptions(
        FamilySharedAccountOperationViewModel model)
    {
        model.OperationKinds =
        [
            new SelectListItem(
                PersonalTransactionKinds.GetNamePl(
                    PersonalTransactionKinds.Income),
                PersonalTransactionKinds.Income),
            new SelectListItem(
                PersonalTransactionKinds.GetNamePl(
                    PersonalTransactionKinds.Expense),
                PersonalTransactionKinds.Expense)
        ];

        model.Categories = PersonalFinanceCategories.All
            .Select(x =>
                new SelectListItem(
                    x.NamePl,
                    x.Code))
            .ToList();
    }


    private static PayFamilyChildContributionViewModel BuildPayChildContributionViewModel(
        FamilyChildContributionPaymentForm form,
        int year,
        int month)
    {
        var model =
            new PayFamilyChildContributionViewModel
            {
                FamilyGroupId = form.FamilyGroupId,
                ObligationId = form.ObligationId,
                Year = year,
                Month = month,
                PaidAtUtc = DateTime.Today
            };

        RebuildPayChildContributionViewModel(
            model,
            form);

        return model;
    }

    private static void RebuildPayChildContributionViewModel(
        PayFamilyChildContributionViewModel model,
        FamilyChildContributionPaymentForm form)
    {
        model.FamilyGroupName =
            form.FamilyGroupName;
        model.ChildDisplayName =
            form.ChildDisplayName;
        model.PeriodKey =
            form.PeriodKey;
        model.Amount =
            form.Amount;
        model.OutstandingAmount =
            form.OutstandingAmount;
        model.DueDateUtc =
            form.DueDateUtc;
        model.TargetHouseholdAccountName =
            form.TargetHouseholdAccountName;
        model.CurrencyCode =
            form.CurrencyCode;

        model.Sources =
            form.Sources
                .Select(x =>
                {
                    var suffix =
                        x.SourceType ==
                            FamilyChildContributionPaymentSourceTypes.HouseholdAccount &&
                        x.IsTargetHouseholdAccount
                            ? " · środki już na koncie docelowym"
                            : string.Empty;

                    return new SelectListItem(
                        $"{x.SourceTypeNamePl}: {x.AccountName} · saldo {x.Balance:N2} {x.CurrencyCode}{suffix}",
                        $"{x.SourceType}|{x.SourceId:D}");
                })
                .ToList();
    }

    private static bool TryParseChildContributionSourceKey(
        string? value,
        out string? sourceType,
        out Guid sourceId)
    {
        sourceType =
            null;
        sourceId =
            Guid.Empty;

        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        var parts =
            value.Split(
                '|',
                2,
                StringSplitOptions.TrimEntries);

        if (parts.Length != 2 ||
            !FamilyChildContributionPaymentSourceTypes.IsValid(
                parts[0]) ||
            !Guid.TryParse(
                parts[1],
                out sourceId))
        {
            return false;
        }

        sourceType =
            parts[0];

        return true;
    }


    private static ConfirmFamilyChildIncomeReceiptViewModel BuildConfirmChildIncomeViewModel(
        FamilyChildIncomeReceiptForm form)
    {
        var model = new ConfirmFamilyChildIncomeReceiptViewModel
        {
            FamilyGroupId = form.FamilyGroupId,
            RuleId = form.RuleId,
            Year = form.Year,
            Month = form.Month,
            ReceivedAtUtc = DateTime.Today
        };

        RebuildConfirmChildIncomeViewModel(model, form);
        return model;
    }

    private static void RebuildConfirmChildIncomeViewModel(
        ConfirmFamilyChildIncomeReceiptViewModel model,
        FamilyChildIncomeReceiptForm form)
    {
        model.FamilyGroupName = form.FamilyGroupName;
        model.BeneficiaryDisplayName = form.BeneficiaryDisplayName;
        model.RuleName = form.RuleName;
        model.IncomeKindNamePl = form.IncomeKindNamePl;
        model.Amount = form.Amount;
        model.PlannedDateUtc = form.PlannedDateUtc;
        model.Accounts = form.Accounts
            .Select(x =>
                new SelectListItem(
                    $"{x.AccountTypeNamePl}: {x.AccountName} · saldo {x.Balance:N2} {x.CurrencyCode}",
                    $"{x.AccountType}|{x.AccountId:D}"))
            .ToList();
    }

    private static bool TryParseIncomeReceiptAccountKey(
        string? value,
        out string? accountType,
        out Guid accountId)
    {
        accountType = null;
        accountId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !FamilyIncomeReceiptAccountTypes.IsValid(parts[0]) ||
            !Guid.TryParse(parts[1], out accountId))
        {
            return false;
        }

        accountType = parts[0];
        return true;
    }

    private static PayFamilyExpenseViewModel BuildPayExpenseViewModel(
        FamilyExpensePaymentForm form,
        int year,
        int month)
    {
        var model = new PayFamilyExpenseViewModel
        {
            FamilyGroupId = form.FamilyGroupId,
            OccurrenceId = form.OccurrenceId,
            Year = year,
            Month = month,
            PaidAtUtc = DateTime.Today
        };

        RebuildPayExpenseViewModel(model, form);
        return model;
    }

    private static void RebuildPayExpenseViewModel(
        PayFamilyExpenseViewModel model,
        FamilyExpensePaymentForm form)
    {
        model.FamilyGroupName = form.FamilyGroupName;
        model.RuleName = form.RuleName;
        model.CategoryNamePl = form.CategoryNamePl;
        model.Amount = form.Amount;
        model.PlannedDateUtc = form.PlannedDateUtc;
        model.BeneficiaryDisplayName = form.BeneficiaryDisplayName;
        model.Accounts = form.Accounts
            .Select(x =>
                new SelectListItem(
                    $"{x.PaymentAccountTypeNamePl}: {x.AccountName} · saldo {x.Balance:N2} {x.CurrencyCode}",
                    $"{x.PaymentAccountType}|{x.AccountId:D}"))
            .ToList();
    }

    private static bool TryParsePaymentAccountKey(
        string? value,
        out string? paymentAccountType,
        out Guid accountId)
    {
        paymentAccountType = null;
        accountId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !FamilyExpensePaymentAccountTypes.IsValid(parts[0]) ||
            !Guid.TryParse(parts[1], out accountId))
        {
            return false;
        }

        paymentAccountType = parts[0];
        return true;
    }

    private static void RebuildMemberOptions(
        AddFamilyMemberViewModel model,
        IReadOnlyList<FamilyMemberCandidate> candidates)
    {
        model.People =
            candidates
                .Select(x =>
                    new SelectListItem(
                        x.DisplayName,
                        x.PersonId.ToString()))
                .ToList();

        model.Roles =
            FamilyRoles.All
                .Select(x =>
                    new SelectListItem(
                        x.NamePl,
                        x.Code))
                .ToList();
    }

    private static string NormalizeFamilyFinanceTab(string? tab) =>
        tab switch
        {
            "members" => "members",
            "children" => "children",
            "child-contributions" => "child-contributions",
            "expenses" => "expenses",
            "shared" => "shared",
            _ => "overview"
        };

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Brak identyfikatora zalogowanego użytkownika.");
    }
}
