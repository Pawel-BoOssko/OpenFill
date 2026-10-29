# OpenFill — instrukcja dla testera

Wersja 0.2 • 29.10.2026

## Jak używać
Uruchom OpenFill, podaj konkretny cel lub przekaż go z ChatGPT/Claude przez MCP. OpenFill sam otwiera strony, wypełnia formularze, loguje się i sprawdza rezultaty.

Kiedy OpenFill czegoś nie wie, najpierw pyta model wywołujący; gdy to nie wystarczy, może wyświetlić pytanie w swoim oknie.

**Przed płatnością OpenFill zapyta o zgodę w swoim oknie.** Dodatkowe potwierdzenie dotyczy końcowego wysłania formularza, którego nie obejmowało zlecenie (jeżeli potwierdzenia są włączone). Wiadomości, formularze i rejestracje jawnie objęte zleceniem są już zatwierdzone.

## Testy
1. Otwórz prostą stronę i poproś o odczytanie tytułu.
2. Na testowym formularzu sprawdź wypełnianie i weryfikację pól.
3. Sprawdź, czy przy braku danych program pyta o konkretny brak.
4. Zgłaszaj komunikat błędu, adres strony, oczekiwany i rzeczywisty rezultat. Nie zamieszczaj haseł ani tokenów w zgłoszeniach.

Do testów używaj formularzy i kont testowych, bez przypadkowych rejestracji lub prawdziwych płatności.
