# Domio M03 — Brama G03

Status początkowy: **DO WERYFIKACJI**

Brama G03 może otrzymać PASS dopiero po spełnieniu wszystkich poniższych warunków.

## Automatyczne
- rozwiązanie kompiluje się bez błędów;
- wszystkie testy M01, M02 i M03 przechodzą;
- `SchemaVersion = 11`;
- brak oczekujących migracji;
- integralność SQLite i FK pozostają poprawne;
- scenariusz `M03_9_ModuleRegressionGateTests` przechodzi.

## Ręczne
1. Utwórz dwa prywatne konta.
2. Dodaj przychód i wydatek z kategorią.
3. Wykonaj transfer pomiędzy własnymi kontami.
4. Spróbuj wydać więcej niż dostępne saldo — operacja ma zostać odrzucona.
5. Utwórz miesięczne wynagrodzenie cykliczne i potwierdź kwotę inną niż planowana.
6. Utwórz subskrypcję cykliczną i potwierdź płatność.
7. Skoryguj błędny wydatek i sprawdź zachowanie operacji źródłowej.
8. Otwórz pełną historię i sprawdź filtry: data, konto, rodzaj, kategoria.
9. Wyzeruj jedno konto transferem i zamknij je — historia ma pozostać.
10. Zaloguj się jako inny domownik — prywatne finanse pierwszej osoby nie mogą być widoczne.

## Oczekiwany health
- module: `M03`
- package: `M03.9`
- schemaVersion: `11`
- pendingMigrations: `0`
- status: `Healthy`

Po potwierdzeniu automatycznych i ręcznych testów:
**G03 = PASS / ACCEPTED**
