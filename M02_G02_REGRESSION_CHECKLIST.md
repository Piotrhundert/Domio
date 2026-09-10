# Domio M02.8 – regresja modułu Użytkownicy i Role przed G02

Cel tej paczki: nie dodaje nowych funkcji biznesowych. Ma potwierdzić, że po M02.1–M02.7 cały moduł M02 działa spójnie przed oznaczeniem G02 jako PASS.

## 1. Kompilacja i testy automatyczne
- [ ] Wyczyść rozwiązanie.
- [ ] Przebuduj rozwiązanie – 0 błędów.
- [ ] Uruchom wszystkie testy – wszystkie zielone.
- [ ] Test `M02_8_ModuleRegressionGateTests.G02_regression_should_preserve_identity_profiles_roles_and_permissions` przechodzi.

## 2. Health / baza
- [ ] `/health` zwraca `status = Healthy`.
- [ ] `schemaVersion = 7`.
- [ ] `pendingMigrations = 0`.
- [ ] `integrityCheck = ok`.
- [ ] `foreignKeysEnabled = true`.

Uwaga: `/health` nadal może pokazywać `package = M02.7`, ponieważ M02.8 jest wyłącznie paczką regresyjną/testową i nie zmienia kodu produkcyjnego ani schematu bazy.

## 3. Logowanie i konto
- [ ] Administrator może się zalogować i wylogować.
- [ ] Błędne hasło nie loguje użytkownika.
- [ ] Konto wyłączone nie może korzystać z aplikacji.
- [ ] Wyłączenie konta nie usuwa ani nie dezaktywuje powiązanej osoby i profilu.
- [ ] Nie można wyłączyć własnego konta Administratora.
- [ ] Nie można odebrać roli ostatniemu aktywnemu Administratorowi.

## 4. Użytkownicy
- [ ] Nowego użytkownika można utworzyć z minimalnych danych: imię, nazwisko, e-mail konta, hasło i rola.
- [ ] Login jest generowany automatycznie.
- [ ] Duplikat loginu otrzymuje kolejny sufiks.
- [ ] Konto i profil są rozdzielone.
- [ ] E-mail konta można zmienić bez zmiany e-maila kontaktowego profilu.

## 5. Profil osoby
- [ ] Administrator może otworzyć profil użytkownika.
- [ ] Użytkownik z `Profile.ViewOwn` widzi własny profil.
- [ ] Użytkownik bez `Profile.ViewAll` nie widzi profilu innej osoby.
- [ ] `Profile.EditOwn` pozwala edytować własny profil.
- [ ] Data urodzenia oblicza wiek.
- [ ] Niepoprawny PESEL jest odrzucany.
- [ ] Data ważności dokumentu wcześniejsza od daty wydania jest odrzucana.
- [ ] Dane zapisane w profilu pozostają po ponownym uruchomieniu aplikacji.
- [ ] Wartość PESEL i numer dokumentu nie trafiają do wpisu audytowego.

## 6. Role i PermissionCode
- [ ] Strona Role pokazuje jedną wybraną rolę.
- [ ] Administrator może zmienić rolę użytkownika.
- [ ] Zmiana roli działa bez restartu aplikacji.
- [ ] Zmiana uprawnień roli działa bez restartu aplikacji.
- [ ] Przywrócenie macierzy domyślnej usuwa ręczną konfigurację praw.
- [ ] Brak PermissionCode blokuje funkcję również po ręcznym wpisaniu URL.
- [ ] Interfejs ukrywa akcje, do których użytkownik nie ma prawa.

## 7. Kryterium G02
G02 można oznaczyć jako PASS dopiero wtedy, gdy:
1. rozwiązanie kompiluje się bez błędów,
2. wszystkie testy są zielone,
3. `/health` jest Healthy,
4. wszystkie powyższe testy ręczne są zaliczone,
5. nie ma otwartych regresji M02.
