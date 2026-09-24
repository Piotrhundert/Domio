namespace Domio.Domain.Notifications;

public sealed record NotificationCodeOption(
    string Code,
    string NamePl);

public static class NotificationSeverityCodes
{
    public const string Info = "Info";
    public const string Important = "Important";
    public const string Warning = "Warning";

    public static readonly IReadOnlyList<NotificationCodeOption> All =
    [
        new(Info, "Informacja"),
        new(Important, "Ważne"),
        new(Warning, "Ostrzeżenie")
    ];

    public static bool IsValid(string? code) =>
        All.Any(x =>
            string.Equals(
                x.Code,
                code,
                StringComparison.Ordinal));

    public static string GetNamePl(string? code) =>
        All.FirstOrDefault(x =>
            string.Equals(
                x.Code,
                code,
                StringComparison.Ordinal))
        ?.NamePl
        ?? code
        ?? "Informacja";
}

public static class NotificationCategoryCodes
{
    public const string System = "System";
    public const string PersonalFinance = "PersonalFinance";
    public const string Household = "Household";
    public const string Contribution = "Contribution";
    public const string Invoice = "Invoice";
    public const string Family = "Family";
    public const string Goal = "Goal";
    public const string User = "User";

    public static readonly IReadOnlyList<NotificationCodeOption> All =
    [
        new(System, "System"),
        new(PersonalFinance, "Finanse osobiste"),
        new(Household, "Finanse domu"),
        new(Contribution, "Składki"),
        new(Invoice, "Faktury"),
        new(Family, "Finanse rodzinne"),
        new(Goal, "Cele"),
        new(User, "Użytkownicy")
    ];

    public static bool IsValid(string? code) =>
        All.Any(x =>
            string.Equals(
                x.Code,
                code,
                StringComparison.Ordinal));

    public static string GetNamePl(string? code) =>
        All.FirstOrDefault(x =>
            string.Equals(
                x.Code,
                code,
                StringComparison.Ordinal))
        ?.NamePl
        ?? code
        ?? "System";
}
