# 📺 Discord Stream Overlay

Lekkie i intuicyjne narzędzie pozwalające na oglądanie streamów z ultra niskim opóźnieniem (**< 100ms**). 

Aplikacja pozwala na „przyklejenie” podglądu wideo bezpośrednio do okna Discorda w systemach Windows (10/11) lub wyświetlenie go w dedykowanym, czystym oknie na systemach Linux. Strumień wideo pobierany jest bezpośrednio od streamera korzystającego z OBS i protokołu WHIP (WebRTC).

---

## ✨ Główne funkcje
- **Ultra niskie opóźnienie** (<100ms) dzięki obsłudze nowoczesnych protokołów.
- **Windows**: Inteligentny overlay renderowany bezpośrednio nad oknem Discorda.
- **Linux**: Dedykowany tryb aplikacji (`--app`) w oparciu o silnik Chromium (czyste okno, bez interfejsu przeglądarki).
- **Zoptymalizowany interfejs**: Izolowane profile przeglądarki i wbudowana "tarcza" (click-shield) zapobiegająca przypadkowemu pauzowaniu wideo.

---

## 🌐 Wymagania sieciowe (Ważne!)
Aplikacja opiera się na bezpośrednim połączeniu (P2P / self-hosted) między serwerem streamera a widzem. Aby narzędzie działało przez Internet, musicie posiadać bezpośrednią widoczność w sieci (VPN lub przekierowanie portów).

---

## 👁️ Po stronie widza (Odbiorcy)

Jako widz potrzebujesz wyłącznie pobrać tę aplikację i wkleić w niej link otrzymany od streamera.

### 🪟 Windows (10 / 11)

1. Pobierz najnowszą wersję `.exe` z zakładki **[Releases](../../releases)**.
2. Uruchom pobrany plik `Discord_Stream_Overlay-1.x.x-Windows-x86_64.exe`.
3. Otwórz aplikację Discord.
4. W zasobniku systemowym (obok zegara) znajdź ikonę aplikacji, kliknij ją prawym przyciskiem myszy i wybierz **Options**.
5. Wklej link otrzymany od streamera (np. `http://192.168.x.x:8889/stream` lub adres z VPN) w polu **Stream URL** i kliknij **Save**.
6. Gdy streamer rozpocznie nadawanie, obraz pojawi się automatycznie na Twoim Discordzie!

> **Uwaga:** Przy pierwszym uruchomieniu filtr Windows SmartScreen może zablokować aplikację. Należy kliknąć *„Więcej informacji”* -> *„Uruchom mimo to”*.

### 🐧 Linux

Wersja na systemy Linux działa jako samodzielna, minimalistyczna aplikacja internetowa z wykorzystaniem silnika Chromium. 

1. Pobierz plik wykonywalny dla systemu Linux z zakładki **[Releases](../../releases)**.
2. Skonfiguruj lub uruchom stream prosto z terminala:

```bash
# Uruchomienie z konkretnym linkiem i rozmiarem okna
./Discord_Stream_Overlay-1.x.x-Linux-x86_64 http://192.168.x.x:8889/stream 1280 720

# Zapisanie samej konfiguracji (bez uruchamiania programu)
./Discord_Stream_Overlay-1.x.x-Linux-x86_64 http://192.168.x.x:8889/stream --save-only
```
3. Przy kolejnych uruchomieniach wystarczy kliknąć plik dwukrotnie (lub uruchomić bez argumentów z terminala) – aplikacja zapamięta ostatnie ustawienia.

### 🛠️ Konfiguracja (Widz)

**Windows (GUI w trayu)**
- **Stream URL** - Adres sieciowy strumienia (do oglądania).
- **Offset X / Offset Y** - Precyzyjne przesunięcie obrazu od lewej/górnej krawędzi okna Discorda (podstawowo skonfigurowane pod monitory 1920x1080).
- **Margin Right / Margin Bottom** - Marginesy ustalające wielkość wideo.
- **Presets 1 / 2 / 3** - Przyciski do szybkiego przełączania się między zapisanymi profilami ustawień.
- **Hotkey** - Skrót klawiszowy do błyskawicznego pokazywania/ukrywania overlayu (domyślnie `F7 + F8`).
*(Po kliknięciu przycisku **Save**, overlay zrestartuje się automatycznie z nowymi parametrami).*

**⚠️ WAŻNE - Wymagania dla Linuksa**
- Do poprawnego działania aplikacji na systemach Linux **wymagane jest zainstalowanie przynajmniej jednej przeglądarki opartej na silniku Chromium** (np. Google Chrome, Chromium, Brave, Microsoft Edge lub Vivaldi). 
- Program wykorzystuje lekki tryb okienkowy (`--app`) tych przeglądarek, tworząc w pełni izolowany profil. Dzięki temu zainstalowane przez Ciebie na co dzień rozszerzenia (np. adblocki) nie ingerują w strumień wideo. Aplikacja posiada także wbudowaną niewidzialną "tarczę", która zabezpiecza wideo przed przypadkowym pauzowaniem po kliknięciu myszką, zostawiając aktywny jedynie dolny pasek sterowania streamem.

**Gdzie zapisywana jest konfiguracja?**
- **Windows:** `%APPDATA%\Discord_Stream_Overlay\config.json`
- **Linux:** `~/.config/discord_stream_overlay/config.json`

---

## 📡 Po stronie streamera (Nadawcy)

Aby widzowie mogli oglądać Twój strumień z ultra niskim opóźnieniem, musisz udostępnić im podgląd przez odpowiedni serwer. 

Działanie narzędzia zostało **oficjalnie potwierdzone z serwerem MediaMTX**. Narzędzie jest jednak uniwersalne i powinno działać z innymi rozwiązaniami, takimi jak:
- ✅ **MediaMTX** (rekomendowane, wbudowana obsługa WebRTC/WHIP)
- **OBS WebRTC**
- **Nginx-RTMP** (z wyjściem HLS - większe opóźnienie)

### Jak skonfigurować nadawanie w OBS Studio (WHIP)

Jeśli korzystasz z serwera np. MediaMTX, konfiguracja OBS Studio jest niezwykle prosta i nie wymaga żadnych dodatkowych wtyczek.

1. Upewnij się, że Twój serwer (np. MediaMTX) jest uruchomiony i gotowy na przyjmowanie połączeń.
2. Otwórz **OBS Studio** i wejdź w **Ustawienia (Settings)**.
3. Przejdź do zakładki **Stream**.
4. Z rozwijanej listy **Serwis (Service)** wybierz **WHIP**.
5. W polu **Serwer** wklej adres nadawania (publish URL). Adres ten zazwyczaj musi zawierać parametr redukujący buforowanie na serwerze do minimum. Powinien wyglądać mniej więcej tak:
   
   `http://192.168.x.x:8889/stream/whip?buffer=0`
   *(Zmień adres IP na swój adres wirtualny z VPN, publiczny lub lokalny, w zależności od wybranej metody połączenia).*

6. Pole **Klucz strumienia (Bearer Token)** możesz zostawić puste.
7. Kliknij **Zastosuj** i **Rozpocznij stream**.

### Konfiguracja wyjścia (Output) w OBS

Aby wszystko działało płynnie i bez błędów w przeglądarkach widzów, nie należy nadmiernie kombinować z nietypowymi enkoderami. Wejdź w zakładkę **Wyjście (Output)** -> **Streaming** i upewnij się, że masz ustawione poniższe parametry:
* **Enkoder wideo:** Zalecany jest **sprzętowy enkoder H.264** (np. NVIDIA NVENC H.264 lub AMD HW H.264). Można użyć kodowania na procesorze (x264), ale wiąże się to ze znacznym obciążeniem CPU i gorszą wydajnością.
* **Enkoder dźwięku:** Wybierz **FFmpeg Opus**.
* **Kontrola przepływności (Rate Control):** Ustaw na **CBR** (Constant Bitrate).

Przykładowa konfiguracja potwierdzona działaniem:

![Konfiguracja Output OBS](tutaj-wklej-link-do-swojego-screena.png)

### Link do oglądania (dla widza)
Pamiętaj, że widzom wklejasz link **do oglądania** (WebRTC/WHEP), a nie ten do nadawania (WHIP). Jeśli wysyłasz w OBS stream na adres z końcówką `/whip`, widz wpisuje w aplikacji `Discord Stream Overlay` adres główny, np.:
`http://192.168.x.x:8889/stream`
