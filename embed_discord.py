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

# ================= KONFIGURACJA =================
MPV_EXE = r"C:\Users\Krata\Home\Programy\mpv-x86_64-v3-20251228-git-a58dd8a\mpv.exe"
WINDOW_TITLE = "MOJ_STREAM" 

# Skrót do chowania obrazu
HOTKEY_TOGGLE_STREAM = "f7+f8"   

# --- ODSTĘPY (Marginesy) ---
OFFSET_X = 325
OFFSET_Y = 38
MARGIN_RIGHT = 8
MARGIN_BOTTOM = 66

TRYB_TESTOWY = False
# ================================================

CMD = [
    MPV_EXE,
    f"--title={WINDOW_TITLE}",
    "--no-keepaspect-window",   
    "--profile=low-latency",
    "--no-cache",
    "--hwdec=auto",
    "--vd-lavc-threads=1",
    "--border=no",
    "--force-window=immediate",
    "--idle=yes",
    "--keep-open=yes",
    "--mute=yes",
    "--no-osc",
    "--input-conf=input.conf",
    "--script=scripts/latency.lua"
]

if TRYB_TESTOWY:
    CMD.append("--idle")
    CMD.append("--background-color=0.2/0.2/0.2") 
else:
    SOURCE = "srt://192.168.8.122:8890?streamid=read:kolega&mode=caller&latency=50000"
    #SOURCE = "srt://0.0.0.0:10000?mode=listener&latency=50000"
    CMD.append(SOURCE)

# Zmienne globalne
mpv_hwnd = None
discord_hwnd = None
visible = True
console_visible = True
restart_requested = False
quit_requested = False

def log(msg):
    try:
        print(f"[LOG] {msg}")
    except OSError:
        pass 

def get_console_window():
    return ctypes.windll.kernel32.GetConsoleWindow()

def disable_close_button():
    """Wyłącza przycisk X, ale po cichu (bez błędów)"""
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
    """Inteligentne zarządzanie konsolą"""
    global console_visible
    hwnd = get_console_window()
    
    # CASE 1: Konsola nie istnieje - tworzymy nową
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

    # CASE 2: Konsola istnieje - chowamy lub pokazujemy
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
    # USUNIĘTO: Linijki wymuszające pokazanie konsoli.
    # Teraz restart jest cichy.

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

def kill_old_mpv():
    subprocess.run(f"taskkill /F /FI \"WINDOWTITLE eq {WINDOW_TITLE}\"", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run("taskkill /F /IM mpv.exe", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

def find_discord():
    def callback(hwnd, result):
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            if " - Discord" in title or title == "Discord":
                rect = win32gui.GetWindowRect(hwnd)
                if (rect[2] - rect[0]) > 200: 
                    result.append(hwnd)
    wins = []
    win32gui.EnumWindows(callback, wins)
    return wins[0] if wins else None

def toggle_hide():
    global visible, mpv_hwnd
    if not mpv_hwnd: return
    if visible:
        win32gui.ShowWindow(mpv_hwnd, win32con.SW_HIDE)
        visible = False
    else:
        win32gui.ShowWindow(mpv_hwnd, win32con.SW_SHOW)
        visible = True

def monitor_mpv_output(proc):
    try:
        for line in iter(proc.stdout.readline, b''):
            if not line: break
            msg = line.decode('utf-8', errors='ignore').strip()
            if "input.conf" not in msg:
                try:
                    print(f"[MPV] {msg}")
                except:
                    pass
    except Exception:
        pass
    finally:
        proc.stdout.close()

def run_stream_cycle():
    global mpv_hwnd, discord_hwnd, restart_requested
    
    restart_requested = False
    mpv_hwnd = None
    
    log("=== START CYKLU ===")
    kill_old_mpv()
    time.sleep(0.5)

    log("Szukam Discorda...")
    discord_hwnd = find_discord()
    if not discord_hwnd:
        log("BLAD: Nie znaleziono Discorda. Czekam 5 sekund...")
        time.sleep(5)
        return 

    log(f"Discord ID: {discord_hwnd}. Start MPV...")
    
    try:
        mpv_process = subprocess.Popen(CMD, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    except FileNotFoundError:
        log("BLAD: Brak pliku mpv.exe!")
        time.sleep(5)
        return

    t = threading.Thread(target=monitor_mpv_output, args=(mpv_process,))
    t.daemon = True
    t.start()

    attempts = 0
    while not mpv_hwnd and attempts < 50:
        time.sleep(0.2)
        if mpv_process.poll() is not None:
            log("BLAD: MPV sie zamknal.")
            return
        mpv_hwnd = win32gui.FindWindow(None, WINDOW_TITLE)
        attempts += 1
    
    if not mpv_hwnd:
        log("BLAD: Okno MPV nie powstalo.")
        mpv_process.terminate()
        return

    log("Ustawiam Wlasciciela...")
    try:
        win32gui.SetWindowLong(mpv_hwnd, win32con.GWL_HWNDPARENT, discord_hwnd)
    except Exception as e:
        log(f"Blad przy SetWindowLong: {e}")
    
    log("Gotowe. Stream dziala.")

    while mpv_process.poll() is None:
        if quit_requested:
            mpv_process.terminate()
            return
        
        if restart_requested:
            log("Restartuje proces MPV...")
            mpv_process.terminate()
            return 

        if not win32gui.IsWindow(discord_hwnd):
            log("Discord zamkniety.")
            mpv_process.terminate()
            return

        # Logika widoczności
        if win32gui.IsIconic(discord_hwnd):
            if win32gui.IsWindowVisible(mpv_hwnd):
                win32gui.ShowWindow(mpv_hwnd, win32con.SW_HIDE)
        else:
            if visible:
                if not win32gui.IsWindowVisible(mpv_hwnd):
                        win32gui.ShowWindow(mpv_hwnd, win32con.SW_SHOW)

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
                            mpv_hwnd, 
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
                if win32gui.IsWindowVisible(mpv_hwnd):
                    win32gui.ShowWindow(mpv_hwnd, win32con.SW_HIDE)

        time.sleep(0.01)

def main_loop_thread():
    keyboard.add_hotkey(HOTKEY_TOGGLE_STREAM, toggle_hide)
    
    # Próba ukrycia konsoli po starcie
    time.sleep(1)
    
    disable_close_button()
    toggle_console() # To powinno ją ukryć
    
    try:
        while not quit_requested:
            run_stream_cycle()
            if not quit_requested:
                time.sleep(1)
    finally:
        kill_old_mpv()
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