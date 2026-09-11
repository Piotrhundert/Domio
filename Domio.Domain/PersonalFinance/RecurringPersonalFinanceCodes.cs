namespace Domio.Domain.PersonalFinance;

public static class PersonalRecurringFrequencies
{
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string Yearly = "Yearly";

    public static readonly IReadOnlyList<PersonalFinanceCodeItem> All =
    [
        new(Monthly, "Co miesiąc"),
        new(Quarterly, "Co 3 miesiące"),
        new(Yearly, "Co rok")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}

public static class PersonalRecurringOccurrenceStatuses
{
    public const string Planned = "Planned";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static string GetNamePl(string code) =>
        code switch
        {
            Planned => "Planowane",
            Confirmed => "Potwierdzone",
            Cancelled => "Anulowane",
            _ => code
        };
}

public static class PersonalFinanceCategories
{
    public const string Salary = "Salary";
    public const string OtherIncome = "OtherIncome";

    public const string Food = "Food";
    public const string Transport = "Transport";
    public const string Home = "Home";
    public const string Health = "Health";
    public const string Education = "Education";
    public const string Leisure = "Leisure";
    public const string Subscription = "Subscription";
    public const string Phone = "Phone";
    public const string Cloud = "Cloud";
    public const string Insurance = "Insurance";
    public const string HouseholdContribution = "HouseholdContribution";
    public const string OtherExpense = "OtherExpense";

    public static readonly IReadOnlyList<PersonalFinanceCodeItem> All =
    [
        new(Salary, "Wynagrodzenie"),
        new(OtherIncome, "Inny przychód"),
        new(Food, "Żywność"),
        new(Transport, "Transport"),
        new(Home, "Dom"),
        new(Health, "Zdrowie"),
        new(Education, "Edukacja"),
        new(Leisure, "Rozrywka"),
        new(Subscription, "Subskrypcja"),
        new(Phone, "Telefon"),
        new(Cloud, "Chmura / usługa online"),
        new(Insurance, "Ubezpieczenie"),
        new(HouseholdContribution, "Składka na budżet domu"),
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
