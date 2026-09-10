namespace Domio.Domain.HouseholdFinance;

public sealed record HouseholdFinanceCodeItem(
    string Code,
    string NamePl);

public static class HouseholdAccountTypes
{
    public const string Bank = "Bank";
    public const string Cash = "Cash";
    public const string Other = "Other";

    public static readonly IReadOnlyList<HouseholdFinanceCodeItem> All =
    [
        new(Bank, "Konto bankowe"),
        new(Cash, "Gotówka"),
        new(Other, "Inne")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}

public static class HouseholdEntryTypes
{
    public const string OpeningBalance = "OpeningBalance";
    public const string Income = "Income";
    public const string Expense = "Expense";
    public const string Correction = "Correction";
    public const string TransferIn = "TransferIn";
    public const string TransferOut = "TransferOut";
    public const string MemberContribution = "MemberContribution";
    public const string TenantPayment = "TenantPayment";

    public static string GetNamePl(string code) =>
        code switch
        {
            OpeningBalance => "Saldo początkowe",
            Income => "Wpływ",
            Expense => "Wydatek",
            Correction => "Korekta",
            TransferIn => "Transfer przychodzący",
            TransferOut => "Transfer wychodzący",
            MemberContribution => "Wpłata domownika",
            TenantPayment => "Wpłata lokatora",
            _ => code
        };
}

public static class HouseholdFinanceCategories
{
    public const string HouseholdIncome = "HouseholdIncome";
    public const string Groceries = "Groceries";
    public const string Utilities = "Utilities";
    public const string HomeMaintenance = "HomeMaintenance";
    public const string Insurance = "Insurance";
    public const string Taxes = "Taxes";
    public const string OtherExpense = "OtherExpense";

    public static readonly IReadOnlyList<HouseholdFinanceCodeItem> All =
    [
        new(HouseholdIncome, "Wpływy gospodarstwa"),
        new(Groceries, "Żywność i zakupy domowe"),
        new(Utilities, "Media i rachunki"),
        new(HomeMaintenance, "Dom i utrzymanie"),
        new(Insurance, "Ubezpieczenia"),
        new(Taxes, "Podatki i opłaty"),
        new(OtherExpense, "Inny wydatek")
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

public static class HouseholdFinanceMoney
{
    public static long ToMinorUnits(decimal amount)
    {
        var rounded =
            decimal.Round(
                amount,
                2,
                MidpointRounding.AwayFromZero);

        if (rounded != amount)
        {
            throw new ArgumentException(
                "Kwota może mieć maksymalnie dwa miejsca po przecinku.");
        }

        return checked(
            decimal.ToInt64(
                rounded * 100m));
    }

    public static decimal FromMinorUnits(long amountMinor) =>
        amountMinor / 100m;
}
