# ?? WhipCast

Lekkie i intuicyjne narzedzie pozwalajace na ogladanie streamów z ultra niskim opóznieniem (**< 100ms**). 

Aplikacja pozwala na „przyklejenie” podgladu wideo bezposrednio do okna whip-casta w systemach Windows (10/11) lub wyswietlenie go w dedykowanym, czystym oknie na systemach Linux. Strumien wideo pobierany jest bezposrednio od streamera korzystajacego z OBS i protokolu WHIP (WebRTC) / HLS.

---

## ? Glówne funkcje
- **Ultra niskie opóznienie** (<100ms) dzieki obsludze nowoczesnych protokolów.
- **Windows**: Inteligentny overlay renderowany bezposrednio nad oknem whip-casta.
- **Linux**: Dedykowany tryb aplikacji (`--app`) w oparciu o silnik Chromium (czyste okno, bez interfejsu przegladarki).
- **Zoptymalizowany interfejs**: Izolowane profile przegladarki i wbudowana "tarcza" (click-shield) zapobiegajaca przypadkowemu pauzowaniu wideo.

---

## ?? Szybki start (dla widza)

### ?? Windows (10 / 11)

1. Pobierz najnowsza wersje `.exe` z zakladki **[Releases](../../releases)**.
2. Uruchom pobrany plik `whip-cast.exe`.
3. Otwórz aplikacje whip-cast.
4. W zasobniku systemowym (obok zegara) znajdz ikone aplikacji, kliknij ja prawym przyciskiem myszy i wybierz **Options**.
5. Wklej link otrzymany od streamera w polu **Stream URL** i kliknij **Save**.
6. Gdy streamer rozpocznie nadawanie, obraz pojawi sie automatycznie na Twoim whip-castzie!

> **Uwaga:** Przy pierwszym uruchomieniu filtr Windows SmartScreen moze zablokowac aplikacje. Nalezy kliknac *„Wiecej informacji”* -> *„Uruchom mimo to”*.

### ?? Linux

Wersja na systemy Linux dziala jako samodzielna, minimalistyczna aplikacja internetowa z wykorzystaniem silnika Chromium. 

1. Pobierz plik wykonywalny dla systemu Linux (np. AppImage) z zakladki **[Releases](../../releases)**.
2. Nadaj mu prawa do wykonywania: `chmod +x whip-cast-linux`.
3. Skonfiguruj lub uruchom stream prosto z terminala:

```bash
# Uruchomienie z konkretnym linkiem i rozmiarem okna
./whip-cast-linux http://link-do-streamu/stream 1280 720

# Zapisanie samej konfiguracji (bez uruchamiania)
./whip-cast-linux http://link-do-streamu/stream --save-only
```
4. Przy kolejnych uruchomieniach wystarczy kliknac plik dwukrotnie (lub uruchomic bez argumentów) – aplikacja zapamieta ostatnie ustawienia.

---

## ??? Konfiguracja i dzialanie

### Windows (GUI w trayu)
- **Stream URL** - Adres sieciowy strumienia.
- **Offset X / Offset Y** - Precyzyjne przesuniecie obrazu od lewej/górnej krawedzi okna whip-casta.
- **Margin Right / Margin Bottom** - Marginesy ustalajace wielkosc wideo.
- **Presets 1 / 2 / 3** - Przyciski do szybkiego przelaczania sie miedzy zapisanymi profilami ustawien.
- **Hotkey** - Skrót klawiszowy do blyskawicznego pokazywania/ukrywania overlayu (domyslnie `F7 + F8`).

*Po kliknieciu przycisku **Save**, overlay zrestartuje sie automatycznie z nowymi parametrami.*

### Linux (Szczególy techniczne i CLI)
Wersja linuksowa posiada kilka zaawansowanych mechanizmów pod maska:
- **Wymagania:** Do dzialania wymagana jest dowolna przegladarka oparta na Chromium (Chrome, Chromium, Brave, Edge, Vivaldi). Jesli nie zostanie znaleziona, program awaryjnie otworzy link w domyslnej przegladarce systemu.
- **Izolowany profil:** Aplikacja tworzy wlasny profil przegladarki. Dzieki temu Twoje wtyczki (np. adblocki, wtyczki cashback) nie ingeruja w strumien i nie psuja okna.
- **Tarcza klikniec (Click-Shield):** Wygenerowany odtwarzacz posiada nalozona niewidzialna warstwe ochronna. Blokuje ona przypadkowe klikniecia (i pauzowanie) na srodku wideo, ale zostawia wolne 52 piksele na dole ekranu, pozwalajac na swobodne korzystanie z paska glosnosci czy trybu pelnoekranowego.

**Dostepne parametry CLI (Linux):**
Skladnia: `[URL] [Szerokosc][Wysokosc] [--save-only]`

Mozesz je dowolnie mieszac, np.:
- Zmiana samego rozmiaru okna: `./app 1920 1080`
- Zmiana samego linku i zapis: `./app http://nowy-link --save-only`

### ?? Gdzie zapisywana jest konfiguracja?
Zarówno na Windowsie, jak i na Linuxie, Twoje ustawienia zapisywane sa w pliku `config.json`:
- **Windows:** `%APPDATA%\whip-cast\config.json`
- **Linux:** `~/.config/whip-cast/config.json`

---

## ?? Wymagania po stronie streamera

Aby to narzedzie zadzialalo, osoba nadajaca (streamer) musi wygenerowac i udostepnic Ci link do strumienia webowego (WebRTC lub HLS). Narzedzie jest kompatybilne z takimi rozwiazaniami jak:
- **MediaMTX**
- **OBS WebRTC**
- **Nginx-RTMP** (z wyjsciem HLS)

Jako widz potrzebujesz wylacznie otrzymanego od streamera adresu URL (np. `http://192.168.x.x:8889/stream`).