namespace Domio.Domain.FamilyFinance;

public sealed record FamilyFinanceCodeItem(
    string Code,
    string NamePl);

public static class FamilyRoles
{
    public const string Adult = "Adult";
    public const string Child = "Child";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(Adult, "Dorosły"),
        new(Child, "Dziecko")
    ];

    public static bool IsValid(string code) =>
        All.Any(x =>
            string.Equals(
                x.Code,
                code,
                StringComparison.Ordinal));

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x =>
            string.Equals(
                x.Code,
                code,
                StringComparison.Ordinal))?.NamePl
        ?? code;
}



public static class FamilySharedAccountRoles
{
    public const string Owner = "Owner";
    public const string CoOwner = "CoOwner";
    public const string Viewer = "Viewer";

    public static string GetNamePl(string code) =>
        code switch
        {
            Owner => "Właściciel",
            CoOwner => "Współwłaściciel",
            Viewer => "Członek rodziny",
            _ => code
        };
}

public static class FamilyBudgetSourceTypes
{
    public const string PersonalTransaction = "PersonalTransaction";
    public const string HouseholdEntry = "HouseholdEntry";
    public const string PersonalRecurringRule = "PersonalRecurringRule";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(PersonalTransaction, "Wydatek prywatny"),
        new(HouseholdEntry, "Wydatek domu"),
        new(PersonalRecurringRule, "Prywatna reguła cykliczna")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}

public static class FamilyRecurringRuleTypes
{
    public const string Expense = "Expense";
    public const string Income = "Income";
}

public static class FamilyIncomeKinds
{
    public const string ChildBenefit800Plus = "ChildBenefit800Plus";
    public const string CareAllowance = "CareAllowance";
    public const string CareBenefit = "CareBenefit";
    public const string Other = "Other";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(ChildBenefit800Plus, "Świadczenie wychowawcze 800+"),
        new(CareAllowance, "Zasiłek pielęgnacyjny"),
        new(CareBenefit, "Świadczenie pielęgnacyjne"),
        new(Other, "Inny przychód związany z dzieckiem")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;

    public static decimal? GetSuggestedAmount(string code) =>
        code switch
        {
            ChildBenefit800Plus => 800m,
            CareAllowance => 215.84m,
            CareBenefit => 3386m,
            _ => null
        };
}

public static class FamilyRecurringFrequencies
{
    public const string Once = "Once";
    public const string Monthly = "Monthly";
    public const string Every2Months = "Every2Months";
    public const string Quarterly = "Quarterly";
    public const string Every4Months = "Every4Months";
    public const string SemiAnnual = "SemiAnnual";
    public const string Yearly = "Yearly";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(Once, "Jednorazowo"),
        new(Monthly, "Co miesiąc"),
        new(Every2Months, "Co 2 miesiące"),
        new(Quarterly, "Co 3 miesiące"),
        new(Every4Months, "Co 4 miesiące"),
        new(SemiAnnual, "Co 6 miesięcy"),
        new(Yearly, "Co rok")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}

public static class FamilyRecurringOccurrenceStatuses
{
    public const string Planned = "Planned";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";

    public static string GetNamePl(string code) =>
        code switch
        {
            Planned => "Do opłacenia",
            Paid => "Opłacono",
            Cancelled => "Anulowano",
            _ => code
        };
}

public static class FamilyBudgetCategories
{
    public const string Food = "Food";
    public const string Home = "Home";
    public const string Transport = "Transport";
    public const string Health = "Health";
    public const string Education = "Education";
    public const string Child = "Child";
    public const string Leisure = "Leisure";
    public const string Subscription = "Subscription";
    public const string Insurance = "Insurance";
    public const string Other = "Other";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(Food, "Żywność"),
        new(Home, "Dom"),
        new(Transport, "Transport"),
        new(Health, "Zdrowie"),
        new(Education, "Edukacja"),
        new(Child, "Dziecko"),
        new(Leisure, "Rozrywka"),
        new(Subscription, "Subskrypcje"),
        new(Insurance, "Ubezpieczenia"),
        new(Other, "Inne")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "Bez kategorii";
        }

        return All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
    }
}

public static class FamilyFinanceMoney
{
    public const int MinorUnitsPerMajorUnit = 100;

    public static long ToMinorUnits(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Kwota musi być większa od zera.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                "Kwota może mieć maksymalnie dwa miejsca po przecinku.",
                nameof(amount));
        }

        return checked((long)(amount * MinorUnitsPerMajorUnit));
    }

    public static long ToMinorUnitsAllowNegative(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                "Kwota może mieć maksymalnie dwa miejsca po przecinku.",
                nameof(amount));
        }

        return checked((long)(amount * MinorUnitsPerMajorUnit));
    }

    public static decimal FromMinorUnits(long amountMinor) =>
        amountMinor / (decimal)MinorUnitsPerMajorUnit;
}

public static class FamilyIncomeReceiptAccountTypes
{
    public const string PersonalAccount = "PersonalAccount";
    public const string HouseholdAccount = "HouseholdAccount";
    public const string FamilySharedAccount = "FamilySharedAccount";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(PersonalAccount, "Konto osobiste"),
        new(HouseholdAccount, "Konto domowe"),
        new(FamilySharedAccount, "Wspólne konto rodziny")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}

public static class FamilyExpensePaymentAccountTypes
{
    public const string PersonalAccount = "PersonalAccount";
    public const string HouseholdAccount = "HouseholdAccount";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(PersonalAccount, "Konto osobiste"),
        new(HouseholdAccount, "Konto domowe")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}
