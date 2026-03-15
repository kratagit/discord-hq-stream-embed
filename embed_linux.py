import sys
import os
import json
import subprocess
import shutil
import tempfile
import urllib.parse
import argparse

# ================= CONFIGURATION =================
APP_NAME = "discord_stream_overlay"
CONFIG_DIR = os.path.join(os.path.expanduser("~"), ".config", APP_NAME)
CONFIG_FILE = os.path.join(CONFIG_DIR, "config.json")

def load_config():
    default_config = {
        "STREAM_URL": "http://192.168.8.122:8889/stream",
        "WINDOW_TITLE": "MY_STREAM",
        "WINDOW_WIDTH": 1280,
        "WINDOW_HEIGHT": 720
    }
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                config_data = json.load(f)
                default_config.update(config_data)
        except Exception as e:
            print(f"Error loading config: {e}")
    return default_config

def save_config(cfg):
    try:
        os.makedirs(CONFIG_DIR, exist_ok=True)
        merged_config = cfg
        if os.path.exists(CONFIG_FILE):
            try:
                with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                    existing = json.load(f)
                    existing.update(cfg)
                    merged_config = existing
            except Exception:
                pass

        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(merged_config, f, indent=4)
    except Exception as e:
        print(f"Error saving config: {e}")

def find_chromium_based_browser():
    """Szuka zainstalowanej przeglądarki opartej na Chromium, bo one obsługują tryb --app."""
    browsers = [
        'google-chrome', 'google-chrome-stable', 'chromium', 'chromium-browser',
        'brave', 'brave-browser', 'microsoft-edge-stable', 'vivaldi'
    ]
    for b in browsers:
        path = shutil.which(b)
        if path:
            return path
    return None

def create_local_html_player(stream_url):
    """Tworzy tymczasowy plik HTML, który osadza stream i dodaje niewidzialną warstwę blokującą kliknięcia."""
    html_content = f"""<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        .container {{ position: relative; width: 100%; height: 100%; }}
        iframe {{ width: 100%; height: 100%; border: none; }}
        
        /* SHIELD: blocks clicking in the center of the image, leaving 52px at the bottom for play/pause/fullscreen buttons */
        .click-shield {{
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: calc(100% - 52px);
            z-index: 999;
        }}
    </style>
</head>
<body>
    <div class="container">
        <!-- Odtwarzacz strumienia -->
        <iframe src="{stream_url}" allow="autoplay; fullscreen; camera; microphone" allowfullscreen="true" webkitallowfullscreen="true" mozallowfullscreen="true"></iframe>
        
        <!-- Niewidzialna warstwa absorbująca kliknięcia w obrazie wideo -->
        <div class="click-shield" oncontextmenu="return false;"></div>
    </div>
</body>
</html>
"""
    fd, path = tempfile.mkstemp(suffix=".html", prefix="discord_stream_linux_")
    with os.fdopen(fd, 'w', encoding='utf-8') as f:
        f.write(html_content)
    # Zwracamy format adresu URL wymaganego do otworzenia pliku lokalnego w przeglądarce
    return f"file://{urllib.parse.quote(path)}"

def main():
    parser = argparse.ArgumentParser(description="Discord Stream Overlay (Linux App Mode)")
    
    # Rejestrujemy pozycyjne, opcjonalne argumenty. Ich kolejność ma znaczenie, nazwa/flaga nie jest wymagana.
    parser.add_argument('url', type=str, nargs='?', default=None, help='Adres URL streamu, np. http://.../stream')
    parser.add_argument('width', type=int, nargs='?', default=None, help='Szerokość okna (np. 1280)')
    parser.add_argument('height', type=int, nargs='?', default=None, help='Wysokość okna (np. 720)')
    
    # Dodatkowa flaga, która pozwala nadpisać config bez uruchamiania UI
    parser.add_argument('--save-only', action='store_true', help='Zapisuje ustawienia i natychmiast wyłącza program')
    
    # Wyłapujemy wszystkie argumenty
    args, unknown = parser.parse_known_args()

    print("Starting Discord Stream Overlay (Linux App Mode)...")
    cfg = load_config()
    
    config_changed = False
    
    # Przypisujemy wartości tylko wtedy, gdy ktoś faktycznie coś podał
    if args.url is not None:
        # Jeśli użytkownik podał liczbę w pierwszej pozycji (np. chce zmienić tylko width), python potraktuje to jako tekst "800".
        # Ale jeśli to jest tylko i wyłącznie liczba, my możemy uznać że to jest tak naprawdę width, a link chce pominąć.
        if args.url.isdigit() and args.width is None:
            cfg["WINDOW_WIDTH"] = int(args.url)
            config_changed = True
        elif args.url.isdigit() and args.width is not None and args.height is None:
             # Np wpisał "800 600", traktujemy to jako width i height.
             cfg["WINDOW_WIDTH"] = int(args.url)
             cfg["WINDOW_HEIGHT"] = int(args.width)
             config_changed = True
        else:
            cfg["STREAM_URL"] = args.url
            config_changed = True
            
            # Skoro `url` było naprawdę linkiem, sprawdzamy też resztę
            if args.width is not None:
                cfg["WINDOW_WIDTH"] = args.width
                config_changed = True
            if args.height is not None:
                cfg["WINDOW_HEIGHT"] = args.height
                config_changed = True

    # Zapisujemy tylko jeśli były jakiekolwiek zmiany z terminala albo jeśli plik jeszcze w ogóle nie istniał
    if config_changed or not os.path.exists(CONFIG_FILE):
        save_config(cfg)
        if config_changed:
            print("Zaktualizowano plik konfiguracji nowymi parametrami z terminala!")

    # Jeżeli użyto flagi --save-only, to przerywamy uruchamianie i od razu zamykamy program
    if args.save_only:
        print("Tylko zapisałem ustawienia, zgodnie z prośbą zamykam aplikację. Miłego dnia!")
        sys.exit(0)

    STREAM_URL = cfg.get("STREAM_URL", "http://192.168.8.122:8889/stream")
    WINDOW_TITLE = cfg.get("WINDOW_TITLE", "Stream Viewer")
    WINDOW_WIDTH = cfg.get("WINDOW_WIDTH", 1280)
    WINDOW_HEIGHT = cfg.get("WINDOW_HEIGHT", 720)

    browser_path = find_chromium_based_browser()
    
    if browser_path:
        print(f"Znaleziono przeglądarkę: {browser_path}")
        print(f"Uruchamiam strumień: {STREAM_URL}")
        
        # Tworzymy lokalny plik HTML z załączonym streamem i tarczą na kliknięcia
        local_player_url = create_local_html_player(STREAM_URL)
        
        # Definiujemy czysty profil dla przeglądarki (żeby wtyczki takie jak LetyShops się nie ładowały)
        profile_dir = os.path.join(CONFIG_DIR, "browser_profile")
        
        # Uruchomienie w trybie "Aplikacji" bez interfejsu przeglądarki ze stałym rozmiarem
        cmd = [
            browser_path,
            f'--app={local_player_url}',
            '--new-window',
            f'--window-size={WINDOW_WIDTH},{WINDOW_HEIGHT}',
            f'--user-data-dir={profile_dir}',
            '--no-first-run',
            '--no-default-browser-check'
        ]
        
        # Otwieramy proces, ukrywając zbędne logi silnika przeglądarki ze strumieni wyjścia
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    else:
        print("Błąd: Nie znaleziono żadnej przeglądarki opartej o Chromium (Chrome, Chromium, Brave, Edge).")
        print("Na Linuksie używamy lekkiego trybu --app, aby osadzić okno bez interfejsu przeglądarki.")
        
        # Awaryjne uruchomienie domyślnej przeglądarki sytemowej w nowym oknie
        import webbrowser
        webbrowser.open_new(STREAM_URL)

if __name__ == '__main__':
    main()
