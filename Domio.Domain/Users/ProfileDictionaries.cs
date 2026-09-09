namespace Domio.Domain.Users;

public sealed record ProfileDictionaryItem(
    string Code,
    string NamePl);

public static class PersonTypes
{
    public const string HouseholdMember = "HouseholdMember";
    public const string Tenant = "Tenant";
    public const string Guest = "Guest";
    public const string Child = "Child";
    public const string Other = "Other";

    public static readonly IReadOnlyList<ProfileDictionaryItem> All =
    [
        new(HouseholdMember, "Domownik"),
        new(Tenant, "Lokator"),
        new(Guest, "Gość"),
        new(Child, "Dziecko"),
        new(Other, "Inna osoba")
    ];

    public static bool IsValid(string? code) =>
        string.IsNullOrWhiteSpace(code) ||
        All.Any(x => string.Equals(x.Code, code, StringComparison.Ordinal));

    public static string? GetNamePl(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal))?.NamePl;
}

public static class IdentityDocumentTypes
{
    public const string IdentityCard = "IdentityCard";
    public const string Passport = "Passport";
    public const string ResidenceCard = "ResidenceCard";
    public const string Other = "Other";

    public static readonly IReadOnlyList<ProfileDictionaryItem> All =
    [
        new(IdentityCard, "Dowód osobisty"),
        new(Passport, "Paszport"),
        new(ResidenceCard, "Karta pobytu"),
        new(Other, "Inny dokument")
    ];

    public static bool IsValid(string? code) =>
        string.IsNullOrWhiteSpace(code) ||
        All.Any(x => string.Equals(x.Code, code, StringComparison.Ordinal));

    public static string? GetNamePl(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal))?.NamePl;
}

public static class PreferredContactMethods
{
    public const string Email = "Email";
    public const string Phone = "Phone";
    public const string Sms = "Sms";
    public const string Application = "Application";
    public const string Other = "Other";

    public static readonly IReadOnlyList<ProfileDictionaryItem> All =
    [
        new(Email, "E-mail"),
        new(Phone, "Telefon"),
        new(Sms, "SMS"),
        new(Application, "Powiadomienie w Domio"),
        new(Other, "Inny")
    ];

    public static bool IsValid(string? code) =>
        string.IsNullOrWhiteSpace(code) ||
        All.Any(x => string.Equals(x.Code, code, StringComparison.Ordinal));

    public static string? GetNamePl(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.Ordinal))?.NamePl;
}
