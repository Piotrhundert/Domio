namespace Domio.Domain.FamilyFinance;

public static class FamilyRecurringDueDateModes
{
    public const string SpecificDay = "SpecificDay";
    public const string StartOfMonth = "StartOfMonth";
    public const string EndOfMonth = "EndOfMonth";
    public const string DaysBeforeEnd = "DaysBeforeEnd";

    public static readonly IReadOnlyList<FamilyFinanceCodeItem> All =
    [
        new(SpecificDay, "Konkretny dzień miesiąca"),
        new(StartOfMonth, "Początek miesiąca"),
        new(EndOfMonth, "Koniec miesiąca"),
        new(DaysBeforeEnd, "Liczba dni przed końcem miesiąca")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;

    public static int ResolveStoredDueDay(
        string modeCode,
        int specificDay,
        int? daysBeforeEnd)
    {
        if (!IsValid(modeCode))
        {
            throw new ArgumentException("Wybierz poprawny sposób wyznaczania terminu.");
        }

        return modeCode switch
        {
            StartOfMonth => 1,
            EndOfMonth => 31,
            DaysBeforeEnd => ResolveDaysBeforeEndStored(daysBeforeEnd),
            SpecificDay => ValidateSpecificDay(specificDay),
            _ => throw new ArgumentException("Wybierz poprawny sposób wyznaczania terminu.")
        };
    }

    public static DateTime ResolveDateUtc(
        string? modeCode,
        int storedDueDay,
        int? daysBeforeEnd,
        int year,
        int month)
    {
        var mode = string.IsNullOrWhiteSpace(modeCode)
            ? SpecificDay
            : modeCode;

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var day = mode switch
        {
            StartOfMonth => 1,
            EndOfMonth => daysInMonth,
            DaysBeforeEnd => Math.Max(
                1,
                daysInMonth - ValidateDaysBeforeEnd(daysBeforeEnd)),
            _ => Math.Min(
                ValidateSpecificDay(storedDueDay),
                daysInMonth)
        };

        return new DateTime(
            year,
            month,
            day,
            0,
            0,
            0,
            DateTimeKind.Utc);
    }

    public static string GetDescription(
        string? modeCode,
        int storedDueDay,
        int? daysBeforeEnd)
    {
        var mode = string.IsNullOrWhiteSpace(modeCode)
            ? SpecificDay
            : modeCode;

        return mode switch
        {
            StartOfMonth => "początek miesiąca",
            EndOfMonth => "koniec miesiąca",
            DaysBeforeEnd => BuildDaysBeforeEndDescription(
                ValidateDaysBeforeEnd(daysBeforeEnd)),
            _ => $"{ValidateSpecificDay(storedDueDay)}. dzień miesiąca"
        };
    }

    private static int ResolveDaysBeforeEndStored(int? days)
    {
        ValidateDaysBeforeEnd(days);
        return 31;
    }

    private static int ValidateSpecificDay(int day)
    {
        if (day < 1 || day > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(day),
                "Dzień miesiąca musi mieścić się w zakresie 1-31.");
        }

        return day;
    }

    private static int ValidateDaysBeforeEnd(int? days)
    {
        if (!days.HasValue || days.Value < 1 || days.Value > 27)
        {
            throw new ArgumentOutOfRangeException(
                nameof(days),
                "Liczba dni przed końcem miesiąca musi mieścić się w zakresie 1-27.");
        }

        return days.Value;
    }

    private static string BuildDaysBeforeEndDescription(int days) =>
        days == 1
            ? "1 dzień przed końcem miesiąca"
            : $"{days} dni przed końcem miesiąca";
}
