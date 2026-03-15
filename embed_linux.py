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
    """Finds an installed Chromium-based browser, because they support --app mode."""
    browsers = {
        'google-chrome': 'Google Chrome',
        'google-chrome-stable': 'Google Chrome',
        'chromium': 'Chromium',
        'chromium-browser': 'Chromium',
        'brave': 'Brave',
        'brave-browser': 'Brave',
        'microsoft-edge-stable': 'Microsoft Edge',
        'vivaldi': 'Vivaldi'
    }
    for cmd, name in browsers.items():
        path = shutil.which(cmd)
        if path:
            return path, name
    return None, None

def create_local_html_player(stream_url, window_title):
    """Creates a temporary HTML file that embeds the stream, adds an invisible click-shield over video, and sets the <title> tag."""
    html_content = f"""<!DOCTYPE html>
<html>
<head>
    <title>{window_title}</title>
    <style>
        body, html {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        .container {{ position: relative; width: 100%; height: 100%; }}
        iframe {{ width: 100%; height: 100%; border: none; }}
        
        /* SHIELD: blocks clicks in the center of the image, leaving 52px at the bottom for play/pause/fullscreen buttons */
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
        <!-- Stream player -->
        <iframe src="{stream_url}" allow="autoplay; fullscreen; camera; microphone" allowfullscreen="true" webkitallowfullscreen="true" mozallowfullscreen="true"></iframe>
        
        <!-- Invisible layer that absorbs clicks on the video area -->
        <div class="click-shield" oncontextmenu="return false;"></div>
    </div>
</body>
</html>
"""
    fd, path = tempfile.mkstemp(suffix=".html", prefix="discord_stream_linux_")
    with os.fdopen(fd, 'w', encoding='utf-8') as f:
        f.write(html_content)
    # Return the URL format required to open the local file in a browser
    return f"file://{urllib.parse.quote(path)}"

def main():
    parser = argparse.ArgumentParser(description="Discord Stream Overlay (Linux App Mode)")
    
    # Register positional optional arguments. Their order matters; names/flags are not required.
    parser.add_argument('url', type=str, nargs='?', default=None, help='Stream URL, e.g. http://.../stream')
    parser.add_argument('width', type=int, nargs='?', default=None, help='Window width (e.g. 1280)')
    parser.add_argument('height', type=int, nargs='?', default=None, help='Window height (e.g. 720)')
    
    # Extra flag to overwrite config without launching the UI
    parser.add_argument('--save-only', action='store_true', help='Saves settings and exits immediately')
    
    # Capture all arguments
    args, unknown = parser.parse_known_args()

    print("Starting Discord Stream Overlay (Linux App Mode)...")
    cfg = load_config()
    
    config_changed = False
    
    # Assign values only when user actually provided them
    if args.url is not None:
        # If the user passed a number in the first position (e.g. wants to change only width), Python treats it as text "800".
        # If it is only a number, we can treat it as width and assume URL should be left unchanged.
        if args.url.isdigit() and args.width is None:
            cfg["WINDOW_WIDTH"] = int(args.url)
            config_changed = True
        elif args.url.isdigit() and args.width is not None and args.height is None:
             # E.g. entered "800 600", treat it as width and height.
             cfg["WINDOW_WIDTH"] = int(args.url)
             cfg["WINDOW_HEIGHT"] = int(args.width)
             config_changed = True
        else:
            cfg["STREAM_URL"] = args.url
            config_changed = True
            
            # Since `url` is actually a URL, check remaining optional values too
            if args.width is not None:
                cfg["WINDOW_WIDTH"] = args.width
                config_changed = True
            if args.height is not None:
                cfg["WINDOW_HEIGHT"] = args.height
                config_changed = True

    # Save only if there were terminal changes or if config file does not exist yet
    if config_changed or not os.path.exists(CONFIG_FILE):
        save_config(cfg)
        if config_changed:
            print("Configuration file updated with new terminal parameters!")

    # If --save-only is used, stop startup and exit immediately
    if args.save_only:
        print("Settings saved only; exiting now as requested.")
        sys.exit(0)

    STREAM_URL = cfg.get("STREAM_URL", "http://192.168.8.122:8889/stream")
    WINDOW_WIDTH = cfg.get("WINDOW_WIDTH", 1280)
    WINDOW_HEIGHT = cfg.get("WINDOW_HEIGHT", 720)

    browser_path, browser_name = find_chromium_based_browser()
    
    if browser_path:
        print(f"Found browser: {browser_name} ({browser_path})")
        print(f"Launching stream: {STREAM_URL}")
        
        # Determine launched file name (if from AppImage, extract proper package name)
        appimage_path = os.environ.get("APPIMAGE")
        if appimage_path:
            app_name = os.path.basename(appimage_path)
        else:
            app_name = os.path.basename(sys.argv[0])
            
        # Build dynamic title: AppName - STREAM_URL (via Browser)
        full_title = f"{app_name} - {STREAM_URL} (via {browser_name})"
        
        # Create local HTML file with embedded stream and click-shield, injecting our title
        local_player_url = create_local_html_player(STREAM_URL, full_title)
        
        # Define a clean browser profile (so extensions like LetyShops are not loaded)
        profile_dir = os.path.join(CONFIG_DIR, "browser_profile")
        
        # Launch in "App" mode without browser UI and with fixed size
        cmd = [
            browser_path,
            f'--app={local_player_url}',
            '--new-window',
            f'--window-size={WINDOW_WIDTH},{WINDOW_HEIGHT}',
            f'--user-data-dir={profile_dir}',
            '--no-first-run',
            '--no-default-browser-check'
        ]
        
        # Start process while hiding unnecessary browser engine logs from output streams
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    else:
        print("Error: No Chromium-based browser found (Chrome, Chromium, Brave, Edge).")
        print("On Linux we use lightweight --app mode to embed a window without browser UI.")
        
        # Fallback: open system default browser in a new window
        import webbrowser
        webbrowser.open_new(STREAM_URL)

if __name__ == '__main__':
    main()
