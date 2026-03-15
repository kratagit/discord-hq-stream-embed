# 📺 Discord Stream Overlay

Lekkie i intuicyjne narzędzie pozwalające na oglądanie streamów z ultra niskim opóźnieniem (**< 100ms**). 

Aplikacja pozwala na „przyklejenie” podglądu wideo bezpośrednio do okna Discorda w systemach Windows (10/11) lub wyświetlenie go w dedykowanym, czystym oknie na systemach Linux. Strumień wideo pobierany jest bezpośrednio od streamera korzystającego z OBS i protokołu WHIP (WebRTC) / HLS.

---

## ✨ Główne funkcje
- **Ultra niskie opóźnienie** (<100ms) dzięki obsłudze nowoczesnych protokołów.
- **Windows**: Inteligentny overlay renderowany bezpośrednio nad oknem Discorda.
- **Linux**: Dedykowany tryb aplikacji (`--app`) w oparciu o silnik Chromium (czyste okno, bez interfejsu przeglądarki).
- **Zoptymalizowany interfejs**: Izolowane profile przeglądarki i wbudowana "tarcza" (click-shield) zapobiegająca przypadkowemu pauzowaniu wideo.

---

## 🚀 Szybki start (dla widza)

### 🪟 Windows (10 / 11)

1. Pobierz najnowszą wersję `.exe` z zakładki **[Releases](../../releases)**.
2. Uruchom pobrany plik `Discord_Stream_Overlay.exe`.
3. Otwórz aplikację Discord.
4. W zasobniku systemowym (obok zegara) znajdź ikonę aplikacji, kliknij ją prawym przyciskiem myszy i wybierz **Options**.
5. Wklej link otrzymany od streamera w polu **Stream URL** i kliknij **Save**.
6. Gdy streamer rozpocznie nadawanie, obraz pojawi się automatycznie na Twoim Discordzie!

> **Uwaga:** Przy pierwszym uruchomieniu filtr Windows SmartScreen może zablokować aplikację. Należy kliknąć *„Więcej informacji”* -> *„Uruchom mimo to”*.

### 🐧 Linux

Wersja na systemy Linux działa jako samodzielna, minimalistyczna aplikacja internetowa z wykorzystaniem silnika Chromium. 

1. Pobierz plik wykonywalny dla systemu Linux (np. AppImage) z zakładki **[Releases](../../releases)**.
2. Nadaj mu prawa do wykonywania: `chmod +x discord_stream_overlay-linux`.
3. Skonfiguruj lub uruchom stream prosto z terminala:

```bash
# Uruchomienie z konkretnym linkiem i rozmiarem okna
./discord_stream_overlay-linux http://link-do-streamu/stream 1280 720

# Zapisanie samej konfiguracji (bez uruchamiania)
./discord_stream_overlay-linux http://link-do-streamu/stream --save-only
```
4. Przy kolejnych uruchomieniach wystarczy kliknąć plik dwukrotnie (lub uruchomić bez argumentów) – aplikacja zapamięta ostatnie ustawienia.

---

## 🛠️ Konfiguracja i działanie

### Windows (GUI w trayu)
- **Stream URL** - Adres sieciowy strumienia.
- **Offset X / Offset Y** - Precyzyjne przesunięcie obrazu od lewej/górnej krawędzi okna Discorda.
- **Margin Right / Margin Bottom** - Marginesy ustalające wielkość wideo.
- **Presets 1 / 2 / 3** - Przyciski do szybkiego przełączania się między zapisanymi profilami ustawień.
- **Hotkey** - Skrót klawiszowy do błyskawicznego pokazywania/ukrywania overlayu (domyślnie `F7 + F8`).

*Po kliknięciu przycisku **Save**, overlay zrestartuje się automatycznie z nowymi parametrami.*

### Linux (Szczegóły techniczne i CLI)
Wersja linuksowa posiada kilka zaawansowanych mechanizmów pod maską:
- **Wymagania:** Do działania wymagana jest dowolna przeglądarka oparta na Chromium (Chrome, Chromium, Brave, Edge, Vivaldi). Jeśli nie zostanie znaleziona, program awaryjnie otworzy link w domyślnej przeglądarce systemu.
- **Izolowany profil:** Aplikacja tworzy własny profil przeglądarki. Dzięki temu Twoje wtyczki (np. adblocki, wtyczki cashback) nie ingerują w strumień i nie psują okna.
- **Tarcza kliknięć (Click-Shield):** Wygenerowany odtwarzacz posiada nałożoną niewidzialną warstwę ochronną. Blokuje ona przypadkowe kliknięcia (i pauzowanie) na środku wideo, ale zostawia wolne 52 piksele na dole ekranu, pozwalając na swobodne korzystanie z paska głośności czy trybu pełnoekranowego.

**Dostępne parametry CLI (Linux):**
Składnia: `[URL] [Szerokość][Wysokość] [--save-only]`

Możesz je dowolnie mieszać, np.:
- Zmiana samego rozmiaru okna: `./app 1920 1080`
- Zmiana samego linku i zapis: `./app http://nowy-link --save-only`

### 📁 Gdzie zapisywana jest konfiguracja?
Zarówno na Windowsie, jak i na Linuxie, Twoje ustawienia zapisywane są w pliku `config.json`:
- **Windows:** `%APPDATA%\Discord_Stream_Overlay\config.json`
- **Linux:** `~/.config/discord_stream_overlay/config.json`

---

## 📡 Wymagania po stronie streamera

Aby to narzędzie zadziałało, osoba nadająca (streamer) musi wygenerować i udostępnić Ci link do strumienia webowego (WebRTC lub HLS). Narzędzie jest kompatybilne z takimi rozwiązaniami jak:
- **MediaMTX**
- **OBS WebRTC**
- **Nginx-RTMP** (z wyjściem HLS)

Jako widz potrzebujesz wyłącznie otrzymanego od streamera adresu URL (np. `http://192.168.x.x:8889/stream`).