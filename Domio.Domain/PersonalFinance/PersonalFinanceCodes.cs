namespace Domio.Domain.PersonalFinance;

public sealed record PersonalFinanceCodeItem(
    string Code,
    string NamePl);

public static class PersonalAccountTypes
{
    public const string BankAccount = "BankAccount";
    public const string SavingsAccount = "SavingsAccount";
    public const string Cash = "Cash";
    public const string Other = "Other";

    public static readonly IReadOnlyList<PersonalFinanceCodeItem> All =
    [
        new(BankAccount, "Konto bankowe"),
        new(SavingsAccount, "Konto oszczędnościowe"),
        new(Cash, "Gotówka"),
        new(Other, "Inne")
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

public static class PersonalTransactionKinds
{
    public const string OpeningBalance = "OpeningBalance";
    public const string Income = "Income";
    public const string Expense = "Expense";
    public const string Correction = "Correction";
    public const string TransferIn = "TransferIn";
    public const string TransferOut = "TransferOut";

    public static string GetNamePl(string code) =>
        code switch
        {
            OpeningBalance => "Saldo początkowe",
            Income => "Przychód",
            Expense => "Wydatek",
            Correction => "Korekta",
            TransferIn => "Transfer przychodzący",
            TransferOut => "Transfer wychodzący",
            _ => code
        };
}

public static class PersonalFinanceMoney
{
    public const int MinorUnitsPerMajorUnit = 100;

    public static long ToMinorUnits(
        decimal amount,
        bool allowNegative = false)
    {
        if (!allowNegative && amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Kwota nie może być ujemna.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                "Kwota może mieć maksymalnie dwa miejsca po przecinku.",
                nameof(amount));
        }

        return checked(
            (long)(amount * MinorUnitsPerMajorUnit));
    }

    public static decimal FromMinorUnits(long amountMinor) =>
        amountMinor / (decimal)MinorUnitsPerMajorUnit;
}
