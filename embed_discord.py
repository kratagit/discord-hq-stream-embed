import win32gui
import win32con
import win32api
import subprocess
import time
import keyboard
import sys
import threading
import ctypes
from PIL import Image, ImageDraw
import pystray
import os
import tempfile

# ================= KONFIGURACJA =================
STREAM_URL = "http://192.168.8.122:8889/stream_legionowo"
WINDOW_TITLE = "MOJ_STREAM" 

# Skrót do chowania obrazu
HOTKEY_TOGGLE_STREAM = "f7+f8"   

# --- ODSTĘPY (Marginesy) ---
OFFSET_X = 325
OFFSET_Y = 38
MARGIN_RIGHT = 8
MARGIN_BOTTOM = 66
# ================================================

# Zmienne globalne
viewer_hwnd = None
discord_hwnd = None
visible = True
console_visible = True
restart_requested = False
quit_requested = False

FS_FLAG = os.path.join(tempfile.gettempdir(), "discord_stream_fs.flag")

def log(msg):
    try:
        print(f"[LOG] {msg}")
    except OSError:
        pass 

def get_console_window():
    return ctypes.windll.kernel32.GetConsoleWindow()

def disable_close_button():
    try:
        hwnd = get_console_window()
        if hwnd:
            hMenu = win32gui.GetSystemMenu(hwnd, False)
            if hMenu:
                win32gui.DeleteMenu(hMenu, win32con.SC_CLOSE, win32con.MF_BYCOMMAND)
                win32gui.DrawMenuBar(hwnd)
    except Exception:
        pass 

def toggle_console(icon=None, item=None):
    global console_visible
    hwnd = get_console_window()
    
    if not hwnd or hwnd == 0:
        try:
            ctypes.windll.kernel32.AllocConsole()
            sys.stdout = open("CONOUT$", "w")
            sys.stderr = open("CONOUT$", "w")
            ctypes.windll.kernel32.SetConsoleTitleW("Logi Discord Stream")
            disable_close_button()
            hwnd = get_console_window()
            try:
                win32gui.SetWindowPos(hwnd, win32con.HWND_TOP, 100, 100, 900, 600, 0x0040)
            except Exception:
                pass
            console_visible = True
            log("Konsola odtworzona.")
        except Exception:
            pass
        return

    try:
        if console_visible:
            win32gui.ShowWindow(hwnd, win32con.SW_HIDE)
            console_visible = False
        else:
            win32gui.ShowWindow(hwnd, win32con.SW_RESTORE)
            disable_close_button()
            try:
                win32gui.SetWindowPos(hwnd, win32con.HWND_TOP, 100, 100, 900, 600, 0x0040)
                win32gui.SetForegroundWindow(hwnd)
            except Exception:
                pass 
            console_visible = True
    except Exception as e:
        log(f"Blad przelaczania okna: {e}")

def trigger_restart(icon=None, item=None):
    global restart_requested
    log("!!! ZAZADANO RESTARTU Z MENU !!!")
    restart_requested = True

def trigger_quit(icon=None, item=None):
    global quit_requested
    log("!!! KONCZENIE PRACY !!!")
    quit_requested = True
    if icon:
        icon.stop()

def create_tray_icon():
    width = 64
    height = 64
    color1 = (88, 101, 242)
    color2 = (255, 255, 255)
    image = Image.new('RGB', (width, height), color1)
    dc = ImageDraw.Draw(image)
    dc.rectangle((width // 4, height // 4, width * 3 // 4, height * 3 // 4), fill=color2)
    return image

def create_webview_script():
    """Generuje skrypt przeglądarki ze 100% pewną metodą flagowania Fullscreena"""
    flag_escaped = FS_FLAG.replace('\\', '\\\\')
    
    script_content = f'''import webview
import sys
import os

FS_FLAG = "{flag_escaped}"

if os.path.exists(FS_FLAG):
    try: os.remove(FS_FLAG)
    except: pass

class Api:
    def set_fs(self, is_fs):
        try:
            if is_fs:
                with open(FS_FLAG, 'w') as f:
                    f.write("1")
            else:
                if os.path.exists(FS_FLAG):
                    os.remove(FS_FLAG)
        except:
            pass

html_content = """<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        iframe {{ width: 100%; height: 100%; border: none; }}
    </style>
</head>
<body>
    <iframe src="{STREAM_URL}" allow="autoplay; fullscreen; camera; microphone" allowfullscreen="true" webkitallowfullscreen="true" mozallowfullscreen="true"></iframe>
    <script>
        let lastFs = false;
        function checkFs() {{
            let isFs = !!document.fullscreenElement;
            if (isFs !== lastFs) {{
                lastFs = isFs;
                if (window.pywebview && window.pywebview.api) {{
                    window.pywebview.api.set_fs(isFs);
                }}
            }}
        }}
        // Reakcja na natywne zdarzenia WebView i zapasowy polling
        document.addEventListener('fullscreenchange', checkFs);
        setInterval(checkFs, 200);
    </script>
</body>
</html>"""

try:
    api = Api()
    window = webview.create_window("{WINDOW_TITLE}", html=html_content, frameless=True, background_color="#000000", js_api=api)
    webview.start()
except Exception as e:
    print(f"WEBVIEW ERROR: {{e}}")
    sys.exit(1)
'''
    script_path = os.path.join(tempfile.gettempdir(), "whep_viewer.py")
    with open(script_path, "w", encoding="utf-8") as f:
        f.write(script_content)
    return script_path

def kill_old_viewers():
    cmd = f'taskkill /F /FI "WINDOWTITLE eq {WINDOW_TITLE}"'
    subprocess.run(cmd, shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

def find_discord():
    def callback(hwnd, result):
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            if " - Discord" in title or title == "Discord":
                rect = win32gui.GetWindowRect(hwnd)
                if (rect[2] - rect[0]) > 200: 
                    result.append(hwnd)
    wins =[]
    win32gui.EnumWindows(callback, wins)
    return wins[0] if wins else None

def find_viewer_window():
    found_hwnds =[]
    def callback(hwnd, _):
        if win32gui.IsWindowVisible(hwnd):
            if win32gui.GetWindowText(hwnd) == WINDOW_TITLE:
                found_hwnds.append(hwnd)
        return True
    win32gui.EnumWindows(callback, None)
    return found_hwnds[0] if found_hwnds else None

def toggle_hide():
    global visible, viewer_hwnd
    if not viewer_hwnd: return
    if visible:
        win32gui.ShowWindow(viewer_hwnd, win32con.SW_HIDE)
        visible = False
    else:
        win32gui.ShowWindow(viewer_hwnd, win32con.SW_SHOW)
        visible = True

def run_stream_cycle():
    global viewer_hwnd, discord_hwnd, restart_requested
    
    restart_requested = False
    viewer_hwnd = None
    is_currently_fs = False
    
    log("=== START CYKLU (WHEP PURE WEBVIEW) ===")
    kill_old_viewers()
    time.sleep(0.5)

    if os.path.exists(FS_FLAG):
        try: os.remove(FS_FLAG)
        except: pass

    log("Szukam Discorda...")
    discord_hwnd = find_discord()
    if not discord_hwnd:
        log("BLAD: Nie znaleziono Discorda. Czekam 5 sekund...")
        time.sleep(5)
        return 

    log(f"Discord ID: {discord_hwnd}. Generuje mini-skrypt playera...")
    viewer_script = create_webview_script()
    
    log("Otwieram proces strumienia...")
    viewer_process = subprocess.Popen([sys.executable, viewer_script], stdout=subprocess.PIPE, stderr=subprocess.STDOUT)

    attempts = 0
    while not viewer_hwnd and attempts < 100:
        time.sleep(0.2)
        if viewer_process.poll() is not None:
            out, _ = viewer_process.communicate()
            error_text = out.decode('utf-8', errors='ignore').strip()
            log(f"CRASH: Skrypt przegladarki umarl natychmiast! Powod:\n{error_text}")
            time.sleep(5) 
            return

        viewer_hwnd = find_viewer_window()
        attempts += 1
    
    if not viewer_hwnd:
        log("BLAD: Okno WebView nie powstalo przez 20 sekund.")
        viewer_process.terminate()
        return

    log("Ustawiam Wlasciciela okna na Discorda...")
    try:
        win32gui.SetWindowLong(viewer_hwnd, win32con.GWL_HWNDPARENT, discord_hwnd)
    except Exception as e:
        log(f"Blad przy SetWindowLong: {e}")
    
    log("Gotowe. Stream dziala.")

    while viewer_process.poll() is None:
        if quit_requested or restart_requested:
            if quit_requested: log("Zamykam strumien (Quit)...")
            else: log("Restartuje proces Playera...")
            try: viewer_process.terminate() 
            except: pass
            kill_old_viewers()
            return

        if not win32gui.IsWindow(discord_hwnd):
            log("Discord zamkniety.")
            try: viewer_process.terminate() 
            except: pass
            kill_old_viewers()
            return

        # ========================================
        # BEZPIECZNA LOGIKA PEŁNEGO EKRANU
        # ========================================
        is_fs_now = os.path.exists(FS_FLAG)
        
        if is_fs_now:
            if not is_currently_fs:
                is_currently_fs = True
                log("Wykryto Pełny Ekran! Odpinam WebView od Discorda.")
                
                # Zdejmujemy wlasciciela (Discord), aby okno mogło zakryć cały ekran niezależnie
                win32gui.SetWindowLong(viewer_hwnd, win32con.GWL_HWNDPARENT, 0)
                
                # Dynamicznie sprawdzamy na którym monitorze jest obecnie aplikacja
                try:
                    # 2 to wartość stałej win32con.MONITOR_DEFAULTTONEAREST
                    monitor = win32api.MonitorFromWindow(discord_hwnd, 2)
                    monitor_info = win32api.GetMonitorInfo(monitor)
                    m_rect = monitor_info['Monitor']
                    m_x, m_y, m_w, m_h = m_rect[0], m_rect[1], m_rect[2] - m_rect[0], m_rect[3] - m_rect[1]
                except Exception:
                    # Awaryjnie Główny Monitor
                    m_x, m_y = 0, 0
                    m_w = win32api.GetSystemMetrics(win32con.SM_CXSCREEN)
                    m_h = win32api.GetSystemMetrics(win32con.SM_CYSCREEN)

                # Wymuszamy nakładkę na cały ekran
                win32gui.SetWindowPos(viewer_hwnd, win32con.HWND_TOPMOST, m_x, m_y, m_w, m_h, win32con.SWP_SHOWWINDOW)
            
            # Gdy ekran jest pełny, 'continue' pomija kod dopasowujący rozmiar pod Discorda
            time.sleep(0.05)
            continue
        else:
            if is_currently_fs:
                is_currently_fs = False
                log("Wyłączono Pełny Ekran. Przypinam z powrotem do Discorda.")
                # Przywracamy nakładkę do okna Discorda
                win32gui.SetWindowLong(viewer_hwnd, win32con.GWL_HWNDPARENT, discord_hwnd)
                # Ściągamy wymuszenie najwyższej warstwy TOPMOST
                win32gui.SetWindowPos(viewer_hwnd, win32con.HWND_NOTOPMOST, 0, 0, 0, 0, win32con.SWP_NOMOVE | win32con.SWP_NOSIZE)


        # Standardowa logika pozycjonowania odtwarzacza w Discordzie
        if win32gui.IsIconic(discord_hwnd):
            if win32gui.IsWindowVisible(viewer_hwnd):
                win32gui.ShowWindow(viewer_hwnd, win32con.SW_HIDE)
        else:
            if visible:
                if not win32gui.IsWindowVisible(viewer_hwnd):
                        win32gui.ShowWindow(viewer_hwnd, win32con.SW_SHOW)

                if not win32gui.IsIconic(discord_hwnd):
                    rect = win32gui.GetWindowRect(discord_hwnd)
                    d_x, d_y = rect[0], rect[1]
                    d_w = rect[2] - rect[0] 
                    d_h = rect[3] - rect[1] 
                    
                    new_width = d_w - OFFSET_X - MARGIN_RIGHT
                    new_height = d_h - OFFSET_Y - MARGIN_BOTTOM
                    
                    if new_width < 100: new_width = 100
                    if new_height < 100: new_height = 100

                    try:
                        win32gui.SetWindowPos(
                            viewer_hwnd, 
                            0, 
                            d_x + OFFSET_X, 
                            d_y + OFFSET_Y, 
                            new_width,     
                            new_height,    
                            win32con.SWP_NOZORDER | win32con.SWP_NOACTIVATE | win32con.SWP_NOOWNERZORDER
                        )
                    except Exception:
                        pass
            else:
                if win32gui.IsWindowVisible(viewer_hwnd):
                    win32gui.ShowWindow(viewer_hwnd, win32con.SW_HIDE)

        time.sleep(0.01)

def main_loop_thread():
    keyboard.add_hotkey(HOTKEY_TOGGLE_STREAM, toggle_hide)
    
    time.sleep(1)
    disable_close_button()
    toggle_console() 
    
    try:
        while not quit_requested:
            run_stream_cycle()
            if not quit_requested:
                time.sleep(1)
    finally:
        kill_old_viewers()
        if os.path.exists(FS_FLAG):
            try: os.remove(FS_FLAG)
            except: pass
        keyboard.unhook_all()
        os._exit(0)

if __name__ == "__main__":
    t = threading.Thread(target=main_loop_thread)
    t.daemon = True
    t.start()

    menu = pystray.Menu(
        pystray.MenuItem("Otwórz/Ukryj Logi", toggle_console, default=True),
        pystray.MenuItem("Restart Stream", trigger_restart),
        pystray.MenuItem("Zakończ", trigger_quit)
    )

    icon = pystray.Icon("DiscordStream", create_tray_icon(), "Discord Stream Overlay", menu)
    
    print("Aplikacja uruchomiona. Sprawdź pasek zadań (tray).")
    try:
        icon.run()
    except Exception as e:
        print(f"Błąd ikony tray: {e}")