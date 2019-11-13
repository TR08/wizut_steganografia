# wizut_steganografia

Program do ukrywania informacji w blikach BMP.

## Program wykorzystuje
- szyfrowanie AES
- kodowanie nadmiarowe (1 z 5)
- permutację rozpraszającą
- SHA512

## Opis kontrolek
### Lewy obrazek
Wyświetla obraz BMP wczytany do aplikacji z pliku.

### Prawy obrazek
Wyświetla obraz z zakodowaną przez program informacją.

### Przycisk "Load image"
Otwiera okno wczytywania obrazka.

### Etykieta obok przycisku "Load image"
Pojawia się po wczytaniu obrazka i zawiera jego pełną ścieżkę. Po dwukliku otwiera folder zawierający plik.

### Przycisk "Save image"
Otwiera okno zapisywania obrazka.

### Etykieta obok przycisku "Save image"
Pojawia się po zapisaniu obrazka i zawiera jego pełną ścieżkę. Po dwukliku otwiera folder zawierający plik.

### Checkbox "Read message from loaded file"
Zaznaczony - program odczytuje wiadomość z obrazka po lewej.  
Odznaczony - program odczytuje wiadomość z obrazka po prawej.

### Przycisk "Put msg"
Umieszcza wiadomość w obrazku z lewej. Obrazek z zamieszczoną wiadomością pojawia się po prawej.

### Przycisk "Read msg"
Odczytuje wiadomość z obrazka (zgodnie z wyborem w checkboxie).

### Etykieta obok przycisku "Read msg"
Wyświetla komunikatu o powodzeniu bądź niepowodzeniu wszelkich operacji.

### Pole tekstowe wiadomości (pod przyciskiem "Read msg")
W polu tym:
- wpisujemy wiadomość do umieszczenia w obrazku,
- umieszczana jest wiadomość odczytana z obrazka,
- umieszczane są informacje o błędach.

### Pole klucza szyfrującego
Pole na wprowadzenie klucza szyfrującego. Zawiera domyślną wartość.

### Pole klucza steganograficznego
Pole na wprowadzenie klucza używanego przez program przy rozsiewaniu wiadomości w obrazie.

## Przykłady
W katalogu projektu znajduje się przykładowy plik "lenna.bmp" oraz pliki z już zakodowanymi wiadomościami:
- "secret_lenna.bmp":
  - klucz szyfrujący: "mYk3Y",
  - klucz steganograficzny: "mYk3Y",
  - wiadomość: "My secret msg!!! :)",
- "empty_lenna.bmp":
  - klucz szyfrujący: "",
  - klucz steganograficzny: "",
  - wiadomość: "",
- "polish_lenna.bmp":
  - klucz szyfrujący: "hasło",
  - klucz steganograficzny: "okoń",
  - wiadomość: "Cieszy mię ten rym: \"Polak mądr po szkodzie\"; Lecz jeśli prawda i z tego nas zbodzie, Nową przypowieść Polak sobie kupi, Że i przed szkodą, i po szkodzie głupi." (w programie bez znaków ucieczki).
