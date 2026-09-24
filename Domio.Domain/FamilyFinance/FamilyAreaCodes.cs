namespace Domio.Domain.FamilyFinance;

public static class FamilyAreaSystemCodes
{
    public const string Preschool = "Preschool";
    public const string Car = "Car";
    public const string HouseholdContributions = "HouseholdContributions";

    public static string GetNamePl(string? code) =>
        code switch
        {
            Preschool => "Przedszkole",
            Car => "Auto",
            HouseholdContributions => "Składki do domu",
            _ => "Własny obszar"
        };

    public static string GetDescriptionPl(string? code) =>
        code switch
        {
            Preschool =>
                "Czesne, wyżywienie, wycieczki, zajęcia sportowe, basen, robotyka i inne koszty związane z przedszkolem.",
            Car =>
                "Ubezpieczenie, serwis, paliwo, opony, wykup auta i inne koszty związane z samochodem.",
            HouseholdContributions =>
                "Automatyczny podgląd składek domowników i dzieci. Dane pochodzą z modułu Składki i domownicy i nie są księgowane drugi raz.",
            _ =>
                "Własna grupa kosztów rodzinnych."
        };

    public static string GetBadge(string? code) =>
        code switch
        {
            Preschool => "PRZ",
            Car => "AUTO",
            HouseholdContributions => "DOM",
            _ => "WŁASNE"
        };

    public static bool IsSystem(string? code) =>
        code is Preschool or Car or HouseholdContributions;
}
