namespace Domio.Domain.FinancialGoals;

public sealed record FinancialGoalCodeItem(
    string Code,
    string NamePl);

public static class FinancialGoalScopes
{
    public const string Personal = "Personal";
    public const string Household = "Household";
    public const string Family = "Family";

    public static readonly IReadOnlyList<FinancialGoalCodeItem> All =
    [
        new(Personal, "Osobiste"),
        new(Household, "Domowe"),
        new(Family, "Rodzinne")
    ];

    public static bool IsValid(string? code) =>
        All.Any(x => string.Equals(x.Code, code, StringComparison.Ordinal));

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal))?.NamePl
        ?? code;
}

public static class FinancialGoalCategories
{
    public const string Technology = "Technology";
    public const string Vehicle = "Vehicle";
    public const string Home = "Home";
    public const string Travel = "Travel";
    public const string Education = "Education";
    public const string Emergency = "Emergency";
    public const string Other = "Other";

    public static readonly IReadOnlyList<FinancialGoalCodeItem> All =
    [
        new(Technology, "Elektronika / technologia"),
        new(Vehicle, "Auto / transport"),
        new(Home, "Dom"),
        new(Travel, "Wakacje / podróże"),
        new(Education, "Edukacja"),
        new(Emergency, "Rezerwa / bezpieczeństwo"),
        new(Other, "Inny cel")
    ];

    public static bool IsValid(string? code) =>
        All.Any(x => string.Equals(x.Code, code, StringComparison.Ordinal));

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal))?.NamePl
        ?? code;
}

public static class FinancialGoalMoney
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

    public static decimal FromMinorUnits(long amountMinor) =>
        amountMinor / (decimal)MinorUnitsPerMajorUnit;
}
