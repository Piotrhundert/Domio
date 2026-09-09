namespace Domio.Domain.Users;

public sealed record PermissionDescriptor(
    string Code,
    string ModuleCode,
    string ModuleNamePl,
    string NamePl,
    string DescriptionPl,
    string RiskLevel,
    IReadOnlyList<string> AllowedScopes);

public sealed record RolePermissionGrant(
    string PermissionCode,
    string ScopeCode);

public static class PermissionScopes
{
    public const string All = "All";
    public const string Own = "Own";
    public const string OwnRoom = "OwnRoom";
    public const string OwnAgreement = "OwnAgreement";
    public const string Agreement = "Agreement";

    public static string GetNamePl(string scopeCode) =>
        scopeCode switch
        {
            All => "Pełny zakres",
            Own => "Tylko własne dane",
            OwnRoom => "Własny pokój / podlicznik",
            OwnAgreement => "Własna umowa",
            Agreement => "Według aktywnej umowy",
            _ => scopeCode
        };
}

public static class SystemPermissions
{
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersEdit = "Users.Edit";
    public const string UsersDisable = "Users.Disable";
    public const string UsersAssignRole = "Users.AssignRole";
    public const string RolesView = "Roles.View";
    public const string RolesEdit = "Roles.Edit";

    public const string ProfileViewOwn = "Profile.ViewOwn";
    public const string ProfileEditOwn = "Profile.EditOwn";
    public const string ProfileViewAll = "Profile.ViewAll";
    public const string ProfileEditAll = "Profile.EditAll";

    public const string FinancePersonalViewOwn =
        "Finance.Personal.ViewOwn";
    public const string FinancePersonalManageOwn =
        "Finance.Personal.ManageOwn";
    public const string FinanceHouseholdView =
        "Finance.Household.View";
    public const string FinanceHouseholdManage =
        "Finance.Household.Manage";
    public const string FinanceHouseholdApprove =
        "Finance.Household.Approve";

    public const string PropertyView = "Property.View";
    public const string PropertyManage = "Property.Manage";

    public const string RentalView = "Rental.View";
    public const string RentalManage = "Rental.Manage";
    public const string RentalClose = "Rental.Close";

    public const string MetersView = "Meters.View";
    public const string MetersManage = "Meters.Manage";
    public const string MetersAddReading = "Meters.AddReading";

    public const string UtilitiesInvoiceManage =
        "Utilities.InvoiceManage";

    public const string SettlementsViewOwn =
        "Settlements.ViewOwn";
    public const string SettlementsView =
        "Settlements.View";
    public const string SettlementsGenerate =
        "Settlements.Generate";
    public const string SettlementsCorrect =
        "Settlements.Correct";
    public const string SettlementsRegisterPayment =
        "Settlements.RegisterPayment";

    public const string AuditView = "Audit.View";

    public const string HouseholdSettingsView =
        "Household.Settings.View";
    public const string HouseholdSettingsManage =
        "Household.Settings.Manage";

    public const string UtilitiesContractView =
        "Utilities.ContractView";
    public const string UtilitiesContractManage =
        "Utilities.ContractManage";

    public const string NotificationsView =
        "Notifications.View";
    public const string NotificationsManage =
        "Notifications.Manage";

    public static readonly IReadOnlyList<string>
        AdministratorProtectedPermissions =
    [
        UsersView,
        UsersCreate,
        UsersEdit,
        UsersDisable,
        UsersAssignRole,
        RolesView,
        RolesEdit
    ];

    public static readonly IReadOnlyList<PermissionDescriptor> All =
    [
        P(
            UsersView,
            "M02",
            "Użytkownicy i role",
            "Podgląd użytkowników",
            "Pozwala otworzyć rejestr osób i kont użytkowników oraz odczytać podstawowe dane kont, status, rolę i datę ostatniego logowania. Nie daje prawa do tworzenia ani zmiany kont.",
            "Średnie",
            PermissionScopes.All),
        P(
            UsersCreate,
            "M02",
            "Użytkownicy i role",
            "Tworzenie użytkowników",
            "Pozwala tworzyć osoby oraz konta logowania. Obejmuje nadanie początkowego hasła i przypisanie roli podczas tworzenia konta.",
            "Wysokie",
            PermissionScopes.All),
        P(
            UsersEdit,
            "M02",
            "Użytkownicy i role",
            "Edycja użytkowników",
            "Pozwala zmieniać dane profilu i konta, takie jak imię, nazwisko, telefon, e-mail i aktywność. Sama zmiana roli jest dodatkowo kontrolowana przez Users.AssignRole.",
            "Wysokie",
            PermissionScopes.All),
        P(
            UsersDisable,
            "M02",
            "Użytkownicy i role",
            "Blokowanie i wyłączanie kont",
            "Pozwala administracyjnie blokować lub wyłączać konto bez usuwania osoby i jej historii. Operacja powinna być audytowana.",
            "Wysokie",
            PermissionScopes.All),
        P(
            UsersAssignRole,
            "M02",
            "Użytkownicy i role",
            "Przypisywanie ról",
            "Pozwala zmienić rolę użytkownika. Zmiana wpływa na jego efektywne uprawnienia i jest rejestrowana w audycie.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            RolesView,
            "M02",
            "Użytkownicy i role",
            "Podgląd ról i uprawnień",
            "Pozwala oglądać role, ich opisy, przypisanych użytkowników oraz pełną listę PermissionCode wraz z zakresem danych.",
            "Średnie",
            PermissionScopes.All),
        P(
            RolesEdit,
            "M02",
            "Użytkownicy i role",
            "Edycja ról i uprawnień",
            "Pozwala zmieniać nazwę i opis roli oraz zestaw przypisanych PermissionCode. Kod roli pozostaje niezmienny, aby nie uszkodzić reguł systemowych.",
            "Krytyczne",
            PermissionScopes.All),

        P(
            ProfileViewOwn,
            "M02",
            "Profil użytkownika",
            "Podgląd własnego profilu",
            "Pozwala użytkownikowi oglądać własny profil osoby, w tym dane osobowe, kontaktowe, adres korespondencyjny, dokument tożsamości i osobę kontaktową. Nie daje dostępu do profili innych osób.",
            "Wysokie",
            PermissionScopes.Own),
        P(
            ProfileEditOwn,
            "M02",
            "Profil użytkownika",
            "Edycja własnego profilu",
            "Pozwala użytkownikowi uzupełniać i zmieniać dane we własnym profilu. Nie pozwala zmieniać roli, loginu, statusu konta ani profili innych osób.",
            "Wysokie",
            PermissionScopes.Own),
        P(
            ProfileViewAll,
            "M02",
            "Profil użytkownika",
            "Podgląd wszystkich profili",
            "Pozwala oglądać pełne profile osób zapisanych w Domio, również osób bez konta logowania. Uprawnienie obejmuje dane identyfikacyjne i kontaktowe, dlatego powinno być nadawane świadomie.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            ProfileEditAll,
            "M02",
            "Profil użytkownika",
            "Edycja wszystkich profili",
            "Pozwala uzupełniać i zmieniać profile wszystkich osób zapisanych w Domio. Nie zastępuje uprawnień do zarządzania kontem logowania, rolą ani blokadą konta.",
            "Krytyczne",
            PermissionScopes.All),

        P(
            FinancePersonalViewOwn,
            "M03",
            "Finanse osobiste",
            "Podgląd własnych finansów",
            "Pozwala odczytywać wyłącznie prywatne konta, salda i operacje należące do zalogowanej osoby. Nie daje dostępu do finansów innych domowników.",
            "Wysokie",
            PermissionScopes.Own),
        P(
            FinancePersonalManageOwn,
            "M03",
            "Finanse osobiste",
            "Zarządzanie własnymi finansami",
            "Pozwala dodawać i korygować własne przychody, wydatki, transfery, subskrypcje i reguły cykliczne. Zakres pozostaje ograniczony do własnej osoby.",
            "Wysokie",
            PermissionScopes.Own),

        P(
            FinanceHouseholdView,
            "M04",
            "Finanse domu",
            "Podgląd finansów domu",
            "Pozwala oglądać wspólne konta gospodarstwa, wpływy, wydatki, faktury i salda dostępne w danym gospodarstwie. Nie obejmuje cudzych finansów prywatnych.",
            "Wysokie",
            PermissionScopes.All),
        P(
            FinanceHouseholdManage,
            "M04",
            "Finanse domu",
            "Zarządzanie finansami domu",
            "Pozwala wykonywać operacje na finansach gospodarstwa, rejestrować wpływy i wydatki oraz obsługiwać dokumenty finansowe zgodnie z regułami modułu.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            FinanceHouseholdApprove,
            "M04",
            "Finanse domu",
            "Akceptacja wpłat i płatności",
            "Pozwala zatwierdzać operacje wymagające potwierdzenia, w szczególności wpłaty gotówkowe i inne operacje zmieniające rzeczywiste saldo gospodarstwa.",
            "Krytyczne",
            PermissionScopes.All),

        P(
            HouseholdSettingsView,
            "CORE / M04",
            "Ustawienia gospodarstwa",
            "Podgląd ustawień gospodarstwa",
            "Pozwala odczytać konfigurację gospodarstwa oraz wersje polityk wpływających na rozliczenia. Prawo jest niezależne od dostępu do finansów domu.",
            "Średnie",
            PermissionScopes.All),
        P(
            HouseholdSettingsManage,
            "CORE / M04",
            "Ustawienia gospodarstwa",
            "Zmiana ustawień gospodarstwa",
            "Pozwala tworzyć nowe wersje polityk i zmieniać konfigurację wpływającą na przyszłe obliczenia. Nie wolno nadpisywać ustawień użytych w historii.",
            "Krytyczne",
            PermissionScopes.All),

        P(
            PropertyView,
            "M05",
            "Nieruchomości i pokoje",
            "Podgląd nieruchomości",
            "Pozwala oglądać nieruchomości, pokoje i ich podstawowe dane. Dla lokatora zakres może być ograniczony wyłącznie do pokoju wynikającego z aktywnej umowy.",
            "Średnie",
            PermissionScopes.All,
            PermissionScopes.OwnRoom),
        P(
            PropertyManage,
            "M05",
            "Nieruchomości i pokoje",
            "Zarządzanie nieruchomościami",
            "Pozwala tworzyć i zmieniać nieruchomości, pokoje oraz dane strukturalne używane później przez najem, liczniki i rozliczenia.",
            "Wysokie",
            PermissionScopes.All),

        P(
            RentalView,
            "M06",
            "Lokatorzy i umowy",
            "Podgląd najmu",
            "Pozwala oglądać dane umów najmu. Zakres Pełny pokazuje wszystkie dostępne umowy gospodarstwa, a Własna umowa wyłącznie umowę zalogowanego lokatora.",
            "Wysokie",
            PermissionScopes.All,
            PermissionScopes.OwnAgreement),
        P(
            RentalManage,
            "M06",
            "Lokatorzy i umowy",
            "Zarządzanie umowami",
            "Pozwala tworzyć umowy najmu, zmieniać warunki poprzez kontrolowane wersje lub aneksy oraz zarządzać danymi lokatora objętymi najmem.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            RentalClose,
            "M06",
            "Lokatorzy i umowy",
            "Zamykanie umów",
            "Pozwala zakończyć aktywną umowę najmu z zachowaniem historii. Nie oznacza fizycznego usunięcia umowy ani lokatora.",
            "Krytyczne",
            PermissionScopes.All),

        P(
            MetersView,
            "M07 / M08",
            "Liczniki i taryfy",
            "Podgląd liczników i odczytów",
            "Pozwala oglądać liczniki, podliczniki i ich odczyty. Dla lokatora zakres może być ograniczony do podlicznika przypisanego do jego pokoju lub umowy.",
            "Średnie",
            PermissionScopes.All,
            PermissionScopes.OwnRoom),
        P(
            MetersManage,
            "M07 / M08",
            "Liczniki i taryfy",
            "Zarządzanie licznikami i taryfami",
            "Pozwala tworzyć, wymieniać i konfigurować liczniki oraz zarządzać taryfami i ich wersjami używanymi do wyliczeń mediów.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            MetersAddReading,
            "M07",
            "Liczniki",
            "Dodawanie odczytów",
            "Pozwala rejestrować nowe stany liczników. Zakres Według aktywnej umowy ogranicza lokatora do odczytów przewidzianych dla jego umowy.",
            "Wysokie",
            PermissionScopes.All,
            PermissionScopes.Agreement),

        P(
            UtilitiesContractView,
            "M09",
            "Umowy i harmonogramy mediów",
            "Podgląd umów operatorów",
            "Pozwala oglądać umowy z operatorami i dostawcami, cykle fakturowania, stawki, przewidywane terminy oraz aktualne warunki bez prawa ich zmiany.",
            "Średnie",
            PermissionScopes.All),
        P(
            UtilitiesContractManage,
            "M09",
            "Umowy i harmonogramy mediów",
            "Zarządzanie umowami operatorów",
            "Pozwala tworzyć, zmieniać i wersjonować umowy z operatorami lub dostawcami, w tym ceny, cykle faktur i ustawienia przypomnień.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            UtilitiesInvoiceManage,
            "M09",
            "Faktury mediów",
            "Zarządzanie fakturami mediów",
            "Pozwala rejestrować i obsługiwać faktury operatorów za energię, wodę, gaz i inne media oraz powiązać je z finansami gospodarstwa.",
            "Krytyczne",
            PermissionScopes.All),

        P(
            NotificationsView,
            "CORE / M09",
            "Powiadomienia",
            "Podgląd powiadomień",
            "Pozwala odczytywać operacyjne powiadomienia gospodarstwa, np. o oczekiwanej fakturze, terminie płatności lub wymaganym działaniu.",
            "Niskie",
            PermissionScopes.All),
        P(
            NotificationsManage,
            "CORE / M09",
            "Powiadomienia",
            "Zarządzanie powiadomieniami",
            "Pozwala zmieniać reguły przypomnień i oznaczać zdarzenia jako rozwiązane. Nie tworzy ani nie zmienia księgowań finansowych.",
            "Wysokie",
            PermissionScopes.All),

        P(
            SettlementsViewOwn,
            "M10",
            "Rozliczenia lokatorów",
            "Podgląd własnych rozliczeń",
            "Pozwala lokatorowi odczytać wyłącznie jego własne miesięczne rozliczenia, należności, wpłaty, nadpłaty i zaległości.",
            "Wysokie",
            PermissionScopes.Own),
        P(
            SettlementsView,
            "M10",
            "Rozliczenia lokatorów",
            "Podgląd wszystkich rozliczeń",
            "Pozwala uprawnionej osobie oglądać rozliczenia lokatorów w gospodarstwie wraz ze źródłami pozycji i historią.",
            "Wysokie",
            PermissionScopes.All),
        P(
            SettlementsGenerate,
            "M10",
            "Rozliczenia lokatorów",
            "Generowanie rozliczeń",
            "Pozwala uruchamiać miesięczne generowanie rozliczeń lokatorów na podstawie umów, odczytów, taryf, faktur i polityk.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            SettlementsCorrect,
            "M10",
            "Rozliczenia lokatorów",
            "Korekty rozliczeń",
            "Pozwala tworzyć kontrolowane korekty opublikowanych rozliczeń bez kasowania oryginalnej historii i źródeł kwot.",
            "Krytyczne",
            PermissionScopes.All),
        P(
            SettlementsRegisterPayment,
            "M10",
            "Rozliczenia lokatorów",
            "Rejestracja wpłat lokatora",
            "Pozwala rejestrować wpłaty gotówkowe i przelewy lokatora oraz alokować je do należności zgodnie z regułami rozliczeń.",
            "Krytyczne",
            PermissionScopes.All),

        P(
            AuditView,
            "M01 / M02",
            "Audyt i bezpieczeństwo",
            "Podgląd audytu",
            "Pozwala przeglądać historię krytycznych zmian wraz z aktorem, czasem, CorrelationId oraz wartościami przed i po zmianie. Nie ujawnia haseł ani innych sekretów.",
            "Wysokie",
            PermissionScopes.All)
    ];

    public static PermissionDescriptor? Find(string code) =>
        All.FirstOrDefault(
            x => string.Equals(
                x.Code,
                code,
                StringComparison.Ordinal));

    private static PermissionDescriptor P(
        string code,
        string moduleCode,
        string moduleNamePl,
        string namePl,
        string descriptionPl,
        string riskLevel,
        params string[] allowedScopes) =>
        new(
            code,
            moduleCode,
            moduleNamePl,
            namePl,
            descriptionPl,
            riskLevel,
            allowedScopes);
}

public static class SystemRolePermissionMatrix
{
    public static IReadOnlyList<RolePermissionGrant> ForRole(
        string roleCode) =>
        roleCode switch
        {
            SystemRoles.AdministratorCode =>
                BuildAdministrator(),
            SystemRoles.HouseholdMemberCode =>
                BuildHouseholdMember(),
            SystemRoles.TenantCode =>
                BuildTenant(),
            SystemRoles.GuestCode =>
                BuildGuest(),
            SystemRoles.ChildCode =>
                BuildChild(),
            _ =>
                Array.Empty<RolePermissionGrant>()
        };

    private static IReadOnlyList<RolePermissionGrant>
        BuildAdministrator() =>
        SystemPermissions.All
            .Select(permission =>
                new RolePermissionGrant(
                    permission.Code,
                    permission.AllowedScopes.Contains(
                        PermissionScopes.Own,
                        StringComparer.Ordinal)
                        ? PermissionScopes.Own
                        : PermissionScopes.All))
            .ToArray();

    private static IReadOnlyList<RolePermissionGrant>
        BuildHouseholdMember() =>
    [
        new(
            SystemPermissions.ProfileViewOwn,
            PermissionScopes.Own),
        new(
            SystemPermissions.ProfileEditOwn,
            PermissionScopes.Own),
        new(
            SystemPermissions.HouseholdSettingsView,
            PermissionScopes.All),
        new(
            SystemPermissions.FinancePersonalViewOwn,
            PermissionScopes.Own),
        new(
            SystemPermissions.FinancePersonalManageOwn,
            PermissionScopes.Own),
        new(
            SystemPermissions.FinanceHouseholdView,
            PermissionScopes.All),
        new(
            SystemPermissions.PropertyView,
            PermissionScopes.All),
        new(
            SystemPermissions.MetersView,
            PermissionScopes.All),
        new(
            SystemPermissions.MetersAddReading,
            PermissionScopes.All),
        new(
            SystemPermissions.UtilitiesContractView,
            PermissionScopes.All),
        new(
            SystemPermissions.NotificationsView,
            PermissionScopes.All)
    ];

    private static IReadOnlyList<RolePermissionGrant>
        BuildTenant() =>
    [
        new(
            SystemPermissions.ProfileViewOwn,
            PermissionScopes.Own),
        new(
            SystemPermissions.ProfileEditOwn,
            PermissionScopes.Own),
        new(
            SystemPermissions.PropertyView,
            PermissionScopes.OwnRoom),
        new(
            SystemPermissions.RentalView,
            PermissionScopes.OwnAgreement),
        new(
            SystemPermissions.MetersView,
            PermissionScopes.OwnRoom),
        new(
            SystemPermissions.MetersAddReading,
            PermissionScopes.Agreement),
        new(
            SystemPermissions.SettlementsViewOwn,
            PermissionScopes.Own)
    ];

    private static IReadOnlyList<RolePermissionGrant>
        BuildGuest() =>
    [
        new(
            SystemPermissions.ProfileViewOwn,
            PermissionScopes.Own),
        new(
            SystemPermissions.ProfileEditOwn,
            PermissionScopes.Own)
    ];

    private static IReadOnlyList<RolePermissionGrant>
        BuildChild() =>
    [
        new(
            SystemPermissions.ProfileViewOwn,
            PermissionScopes.Own)
    ];
}
