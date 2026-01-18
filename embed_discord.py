import win32gui
import win32con
import subprocess
import time
import keyboard
import sys
import threading

# ================= KONFIGURACJA =================
MPV_EXE = r"C:\Users\Krata\Home\Programy\mpv-x86_64-v3-20251228-git-a58dd8a\mpv.exe"
WINDOW_TITLE = "MOJ_STREAM" 
HOTKEY = "f7+f8"           

# --- ODSTĘPY (Marginesy) ---
OFFSET_X = 325
OFFSET_Y = 38
MARGIN_RIGHT = 8
MARGIN_BOTTOM = 66

TRYB_TESTOWY = True
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
    
    "--no-osc",  # To wyłącza interfejs graficzny (pasek sterowania)

    "--input-conf=input.conf",
    "--script=scripts/latency.lua"
]

if TRYB_TESTOWY:
    CMD.append("--idle")
    CMD.append("--background-color=0.2/0.2/0.2") 
else:
    SOURCE = "srt://0.0.0.0:10000?mode=listener&latency=50000"
    CMD.append(SOURCE)

mpv_hwnd = None
discord_hwnd = None
visible = True

def log(msg):
    print(f"[LOG] {msg}")

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
    for line in iter(proc.stdout.readline, b''):
        msg = line.decode('utf-8', errors='ignore').strip()
        if "input.conf" not in msg:
            print(f"[MPV] {msg}")
    proc.stdout.close()

def main():
    global mpv_hwnd, discord_hwnd
    
    log("Startuje...")
    kill_old_mpv()
    time.sleep(0.5)

    log("Szukam Discorda...")
    discord_hwnd = find_discord()
    if not discord_hwnd:
        log("BLAD: Nie znaleziono Discorda.")
        return

    log(f"Discord ID: {discord_hwnd}. Start MPV...")
    
    try:
        mpv_process = subprocess.Popen(CMD, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    except FileNotFoundError:
        log("BLAD: Brak pliku mpv.exe!")
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

    log("Ustawiam Wlasciciela (Sticky Window)...")
    try:
        win32gui.SetWindowLong(mpv_hwnd, win32con.GWL_HWNDPARENT, discord_hwnd)
    except Exception as e:
        log(f"Blad przy SetWindowLong: {e}")
    
    keyboard.add_hotkey(HOTKEY, toggle_hide)
    log("Gotowe. Rozmiar okna bedzie dynamiczny.")

    try:
        while mpv_process.poll() is None:
            if not win32gui.IsWindow(discord_hwnd):
                break
            
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

                        win32gui.SetWindowPos(
                            mpv_hwnd, 
                            0, 
                            d_x + OFFSET_X, 
                            d_y + OFFSET_Y, 
                            new_width,     
                            new_height,    
                            win32con.SWP_NOZORDER | win32con.SWP_NOACTIVATE | win32con.SWP_NOOWNERZORDER
                        )
                else:
                    if win32gui.IsWindowVisible(mpv_hwnd):
                        win32gui.ShowWindow(mpv_hwnd, win32con.SW_HIDE)

            time.sleep(0.01)

    except KeyboardInterrupt:
        log("Koniec.")
    finally:
        keyboard.unhook_all()
        if mpv_process.poll() is None:
            mpv_process.terminate()
        kill_old_mpv()

if __name__ == "__main__":
    main()