namespace Domio.Domain.HouseholdFinance;

public static class HouseholdContributionModes
{
    public const string FixedAmount = "FixedAmount";
    public const string PercentageOfIncome = "PercentageOfIncome";

    public static readonly IReadOnlyList<HouseholdFinanceCodeItem> All =
    [
        new(FixedAmount, "Stała kwota"),
        new(PercentageOfIncome, "Procent planowanego wynagrodzenia")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}

public static class HouseholdContributionStatuses
{
    public const string Pending = "Pending";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
    public const string Cancelled = "Cancelled";
    public const string Corrected = "Corrected";

    public static string GetNamePl(string code) =>
        code switch
        {
            Pending => "Oczekuje",
            PartiallyPaid => "Częściowo opłacona",
            Paid => "Opłacona",
            Overdue => "Po terminie",
            Cancelled => "Anulowana",
            Corrected => "Skorygowana",
            _ => code
        };
}

public static class HouseholdContributionMath
{
    public static int ToBasisPoints(decimal percentage)
    {
        if (percentage <= 0m ||
            percentage > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentage),
                "Procent składki musi być większy od 0 i nie większy niż 100.");
        }

        if (decimal.Round(percentage, 2) != percentage)
        {
            throw new ArgumentException(
                "Procent składki może mieć maksymalnie dwa miejsca po przecinku.",
                nameof(percentage));
        }

        return checked(
            decimal.ToInt32(
                percentage * 100m));
    }

    public static decimal FromBasisPoints(int basisPoints) =>
        basisPoints / 100m;

    public static long CalculatePercentageAmountMinor(
        long plannedIncomeAmountMinor,
        int percentageBasisPoints)
    {
        if (plannedIncomeAmountMinor < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plannedIncomeAmountMinor));
        }

        if (percentageBasisPoints <= 0 ||
            percentageBasisPoints > 10000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentageBasisPoints));
        }

        var result =
            decimal.Round(
                plannedIncomeAmountMinor *
                    (percentageBasisPoints / 10000m),
                0,
                MidpointRounding.AwayFromZero);

        return checked(
            decimal.ToInt64(
                result));
    }
}

public static class HouseholdContributionPaymentStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static string GetNamePl(string code) =>
        code switch
        {
            Pending => "Oczekuje na akceptację",
            Approved => "Zaakceptowana",
            Rejected => "Odrzucona",
            _ => code
        };
}

public sealed record HouseholdContributionReminderInfo(
    string Code,
    string NamePl,
    int DaysToDue,
    bool IsActive);

public static class HouseholdContributionReminderStates
{
    public const string None = "None";
    public const string Upcoming = "Upcoming";
    public const string DueToday = "DueToday";
    public const string Overdue = "Overdue";

    public static HouseholdContributionReminderInfo Resolve(
        DateTime dueDateUtc,
        int reminderDays,
        long outstandingAmountMinor,
        string statusCode,
        DateTime todayUtc)
    {
        var normalizedReminderDays =
            Math.Clamp(
                reminderDays,
                0,
                31);

        var daysToDue =
            (dueDateUtc.Date -
             todayUtc.Date)
            .Days;

        if (outstandingAmountMinor <= 0 ||
            statusCode ==
                HouseholdContributionStatuses.Paid ||
            statusCode ==
                HouseholdContributionStatuses.Cancelled ||
            statusCode ==
                HouseholdContributionStatuses.Corrected)
        {
            return new HouseholdContributionReminderInfo(
                None,
                string.Empty,
                daysToDue,
                false);
        }

        if (daysToDue < 0)
        {
            var daysOverdue =
                Math.Abs(
                    daysToDue);

            return new HouseholdContributionReminderInfo(
                Overdue,
                daysOverdue == 1
                    ? "Po terminie o 1 dzień"
                    : $"Po terminie o {daysOverdue} dni",
                daysToDue,
                true);
        }

        if (daysToDue == 0)
        {
            return new HouseholdContributionReminderInfo(
                DueToday,
                "Termin składki jest dzisiaj",
                0,
                true);
        }

        if (daysToDue <=
            normalizedReminderDays)
        {
            return new HouseholdContributionReminderInfo(
                Upcoming,
                daysToDue == 1
                    ? "Termin jutro"
                    : $"Termin za {daysToDue} dni",
                daysToDue,
                true);
        }

        return new HouseholdContributionReminderInfo(
            None,
            string.Empty,
            daysToDue,
            false);
    }
}
