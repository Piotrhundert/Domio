namespace Domio.Domain.HouseholdFinance;

public static class HouseholdMemberObligationSourceTypes
{
    public const string Invoice = "Invoice";
    public const string OtherCost = "OtherCost";

    public static readonly IReadOnlyList<HouseholdFinanceCodeItem> All =
    [
        new(Invoice, "Udział w fakturze domu"),
        new(OtherCost, "Inny koszt gospodarstwa")
    ];

    public static bool IsValid(string code) =>
        All.Any(x => x.Code == code);

    public static string GetNamePl(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.NamePl ?? code;
}

public static class HouseholdMemberObligationStatuses
{
    public const string Pending = "Pending";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
    public const string Cancelled = "Cancelled";

    public static string GetNamePl(string code) =>
        code switch
        {
            Pending => "Oczekuje",
            PartiallyPaid => "Częściowo opłacone",
            Paid => "Opłacone",
            Overdue => "Po terminie",
            Cancelled => "Anulowane",
            _ => code
        };
}

public static class HouseholdMemberObligationPaymentStatuses
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
