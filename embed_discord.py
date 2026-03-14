import win32gui
import win32con
import win32api
import subprocess
import time
import keyboard
import sys
import threading
import ctypes
import json
import tkinter as tk
from tkinter import simpledialog, messagebox
from PIL import Image, ImageDraw
import pystray
import os
import tempfile
import webview

# ================= CONFIGURATION =================
APP_NAME = "Discord_Stream_Overlay"
SINGLE_INSTANCE_MUTEX_NAME = "Local\\Discord_Stream_Overlay_SingleInstance_Mutex"
CONFIG_DIR = os.path.join(os.getenv('APPDATA'), APP_NAME) if os.name == 'nt' else os.path.join(os.path.expanduser("~"), "." + APP_NAME)
CONFIG_FILE = os.path.join(CONFIG_DIR, "config.json")

def load_config():
    default_config = {
        "STREAM_URL": "http://192.168.8.122:8889/stream",
        "HOTKEY_TOGGLE_STREAM": "f7+f8",
        "WINDOW_TITLE": "MY_STREAM",
        "OFFSET_X": 325,
        "OFFSET_Y": 38,
        "MARGIN_RIGHT": 8,
        "MARGIN_BOTTOM": 66,
        "PRESETS": {
            "1": {"OFFSET_X": 325, "OFFSET_Y": 38, "MARGIN_RIGHT": 8, "MARGIN_BOTTOM": 66},
            "2": {"OFFSET_X": 325, "OFFSET_Y": 38, "MARGIN_RIGHT": 8, "MARGIN_BOTTOM": 66},
            "3": {"OFFSET_X": 325, "OFFSET_Y": 38, "MARGIN_RIGHT": 8, "MARGIN_BOTTOM": 66}
        }
    }
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                default_config.update(json.load(f))
        except Exception:
            pass
    return default_config

def save_config(cfg):
    try:
        os.makedirs(CONFIG_DIR, exist_ok=True)
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(cfg, f, indent=4)
    except Exception:
        pass

cfg = load_config()
if not os.path.exists(CONFIG_FILE):
    save_config(cfg)

STREAM_URL = cfg["STREAM_URL"]
WINDOW_TITLE = cfg["WINDOW_TITLE"] 

# Skrót do chowania obrazu
HOTKEY_TOGGLE_STREAM = cfg["HOTKEY_TOGGLE_STREAM"]   

# --- ODSTĘPY (Marginesy) ---
OFFSET_X = cfg["OFFSET_X"]
OFFSET_Y = cfg["OFFSET_Y"]
MARGIN_RIGHT = cfg["MARGIN_RIGHT"]
MARGIN_BOTTOM = cfg["MARGIN_BOTTOM"]
# ================================================

# Zmienne globalne
viewer_hwnd = None
discord_hwnd = None
visible = True
console_visible = False
restart_requested = False
quit_requested = False
options_open = False
options_root = None
tray_icon = None

single_instance_mutex = None
ERROR_ALREADY_EXISTS = 183

FS_FLAG = os.path.join(tempfile.gettempdir(), "discord_stream_fs.flag")

def log(msg):
    pass

def suppress_console_output():
    try:
        if os.name == 'nt':
            hwnd = ctypes.windll.kernel32.GetConsoleWindow()
            if hwnd:
                ctypes.windll.kernel32.FreeConsole()
    except Exception:
        pass

    try:
        devnull = open(os.devnull, "w", encoding="utf-8")
        sys.stdout = devnull
        sys.stderr = devnull
    except Exception:
        pass

def ensure_single_instance():
    global single_instance_mutex
    if os.name != 'nt':
        return True

    kernel32 = ctypes.windll.kernel32
    mutex_handle = kernel32.CreateMutexW(None, False, SINGLE_INSTANCE_MUTEX_NAME)
    if not mutex_handle:
        return True

    if kernel32.GetLastError() == ERROR_ALREADY_EXISTS:
        kernel32.CloseHandle(mutex_handle)
        return False

    single_instance_mutex = mutex_handle
    return True

def get_launcher_executable():
    if getattr(sys, 'frozen', False):
        return sys.executable

    python_exe = sys.executable.lower()
    if python_exe.endswith("python.exe"):
        pythonw_exe = sys.executable[:-10] + "pythonw.exe"
        if os.path.exists(pythonw_exe):
            return pythonw_exe
    return sys.executable

def is_parent_process_alive(parent_pid):
    if os.name != 'nt':
        return True
    try:
        kernel32 = ctypes.windll.kernel32
        SYNCHRONIZE = 0x00100000
        process_handle = kernel32.OpenProcess(SYNCHRONIZE, False, parent_pid)
        if not process_handle:
            return False
        WAIT_TIMEOUT = 258
        status = kernel32.WaitForSingleObject(process_handle, 0)
        kernel32.CloseHandle(process_handle)
        return status == WAIT_TIMEOUT
    except Exception:
        return True

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
            sys.stdout = open("CONOUT$", "w", encoding="utf-8")
            sys.stderr = open("CONOUT$", "w", encoding="utf-8")
            ctypes.windll.kernel32.SetConsoleTitleW("Logs")
            disable_close_button()
            hwnd = get_console_window()
            try:
                win32gui.SetWindowPos(hwnd, win32con.HWND_TOP, 100, 100, 900, 600, 0x0040)
            except Exception:
                pass
            console_visible = True
            log("Console restored.")
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
        log(f"Error toggling window: {e}")

def hide_console():
    global console_visible
    hwnd = get_console_window()
    if hwnd and console_visible:
        try:
            win32gui.ShowWindow(hwnd, win32con.SW_HIDE)
            console_visible = False
            log("Hiding console, stream ok.")
        except Exception:
            pass

def trigger_restart(icon=None, item=None):
    global restart_requested
    log("!!! RESTART REQUESTED FROM MENU !!!")
    restart_requested = True

def trigger_global_shutdown(reason=""):
    global quit_requested, tray_icon
    quit_requested = True
    if reason:
        log(reason)
    if tray_icon:
        try:
            tray_icon.stop()
        except Exception:
            pass

def get_resource_path(relative_path):
    """Returns resource path whether running as script or compiled exe"""
    import sys
    import os
    if hasattr(sys, '_MEIPASS'):
        return os.path.join(sys._MEIPASS, relative_path)
    return os.path.join(os.path.abspath("."), relative_path)

def open_options_dialog():
    global STREAM_URL, HOTKEY_TOGGLE_STREAM, toggle_hide, options_open, options_root
    global OFFSET_X, OFFSET_Y, MARGIN_RIGHT, MARGIN_BOTTOM
    
    options_open = True
    
    root = tk.Tk()
    options_root = root
    root.withdraw() # Ukrywamy na moment, by nie "skakało" przy tworzeniu i wyśrodkowywaniu
    root.title("Options (Stream Settings)")
    root.resizable(False, False)
    root.attributes('-topmost', True) # Zawsze na wierzchu do czasu zamknięcia
    
    # Ustawienie ikony okna
    try:
        import sys
        if getattr(sys, 'frozen', False):
            # Pobierz ikonę bezpośrednio z uruchomionego pliku .exe
            root.iconbitmap(sys.executable)
        else:
            icon_path = get_resource_path(os.path.join("assets", "icon.ico"))
            if os.path.exists(icon_path):
                root.iconbitmap(icon_path)
    except Exception:
        pass
            
    font_bold = ("Arial", 10, "bold")
    font_norm = ("Arial", 10)
    
    main_frame = tk.Frame(root, padx=20, pady=15)
    main_frame.pack(fill="both", expand=True)
    
    # Funkcja do zmiany kursora na łapkę po najechaniu na przycisk
    def on_enter(e):
        e.widget['cursor'] = 'hand2'

    def on_leave(e):
        e.widget['cursor'] = ''
    
    # URL
    tk.Label(main_frame, text="Stream address (STREAM_URL):", font=font_bold).pack(anchor="w")
    url_entry = tk.Entry(main_frame, width=65, font=font_norm)
    url_entry.insert(0, STREAM_URL)
    url_entry.pack(fill="x", pady=(5, 15))
    
    # Hotkey
    tk.Label(main_frame, text="Shortcut to hide window (HOTKEY_TOGGLE_STREAM):", font=font_bold).pack(anchor="w")
    hotkey_entry = tk.Entry(main_frame, width=65, font=font_norm)
    hotkey_entry.insert(0, HOTKEY_TOGGLE_STREAM)
    hotkey_entry.pack(fill="x", pady=(5, 15))

    # Marginesy
    pos_frame = tk.LabelFrame(main_frame, text=" Margins and Position (Window Adjustment) ", font=font_bold, padx=15, pady=10)
    pos_frame.pack(fill="x", pady=(5, 10))
    pos_frame.columnconfigure(1, weight=1)
    pos_frame.columnconfigure(3, weight=1)

    tk.Label(pos_frame, text="OFFSET_X:", font=font_norm).grid(row=0, column=0, sticky="e", padx=5, pady=5)
    off_x_entry = tk.Entry(pos_frame, font=font_norm, justify="center")
    off_x_entry.insert(0, str(OFFSET_X))
    off_x_entry.grid(row=0, column=1, sticky="we", padx=5, pady=5)

    tk.Label(pos_frame, text="OFFSET_Y:", font=font_norm).grid(row=0, column=2, sticky="e", padx=5, pady=5)
    off_y_entry = tk.Entry(pos_frame, font=font_norm, justify="center")
    off_y_entry.insert(0, str(OFFSET_Y))
    off_y_entry.grid(row=0, column=3, sticky="we", padx=5, pady=5)

    tk.Label(pos_frame, text="MARGIN_RIGHT:", font=font_norm).grid(row=1, column=0, sticky="e", padx=5, pady=5)
    mar_r_entry = tk.Entry(pos_frame, font=font_norm, justify="center")
    mar_r_entry.insert(0, str(MARGIN_RIGHT))
    mar_r_entry.grid(row=1, column=1, sticky="we", padx=5, pady=5)

    tk.Label(pos_frame, text="MARGIN_BOTTOM:", font=font_norm).grid(row=1, column=2, sticky="e", padx=5, pady=5)
    mar_b_entry = tk.Entry(pos_frame, font=font_norm, justify="center")
    mar_b_entry.insert(0, str(MARGIN_BOTTOM))
    mar_b_entry.grid(row=1, column=3, sticky="we", padx=5, pady=5)

    # Funkcje presetów
    def load_preset(pid):
        preset = cfg.get("PRESETS", {}).get(str(pid))
        if preset:
            off_x_entry.delete(0, tk.END)
            off_x_entry.insert(0, str(preset["OFFSET_X"]))
            off_y_entry.delete(0, tk.END)
            off_y_entry.insert(0, str(preset["OFFSET_Y"]))
            mar_r_entry.delete(0, tk.END)
            mar_r_entry.insert(0, str(preset["MARGIN_RIGHT"]))
            mar_b_entry.delete(0, tk.END)
            mar_b_entry.insert(0, str(preset["MARGIN_BOTTOM"]))
            messagebox.showinfo("Loaded", f"Loaded Preset {pid}", parent=root)

    def save_preset(pid):
        try:
            preset = {
                "OFFSET_X": int(off_x_entry.get()),
                "OFFSET_Y": int(off_y_entry.get()),
                "MARGIN_RIGHT": int(mar_r_entry.get()),
                "MARGIN_BOTTOM": int(mar_b_entry.get())
            }
            if "PRESETS" not in cfg:
                cfg["PRESETS"] = {}
            cfg["PRESETS"][str(pid)] = preset
            save_config(cfg)
            messagebox.showinfo("Saved", f"Saved settings to Preset {pid}", parent=root)
        except ValueError:
            messagebox.showerror("Error", "Margins must be integers!", parent=root)

    # Frame na presety
    preset_frame = tk.LabelFrame(main_frame, text=" Saved Margin Presets ", font=font_bold, padx=10, pady=10)
    preset_frame.pack(fill="x", pady=5)
    
    # Wyśrodkowanie wewnątrz ramki poprzez kontener
    preset_inner = tk.Frame(preset_frame)
    preset_inner.pack(anchor="center")
    
    for i in range(1, 4):
        p_sub = tk.Frame(preset_inner, padx=5)
        p_sub.pack(side=tk.LEFT)
        tk.Label(p_sub, text=f"Preset {i}", font=font_bold).pack(pady=(0, 2))
        bb_frame = tk.Frame(p_sub)
        bb_frame.pack()
        
        btn_load = tk.Button(bb_frame, text="Load", font=font_norm, width=8, command=lambda p=i: load_preset(p))
        btn_load.pack(side=tk.LEFT, padx=2)
        btn_load.bind("<Enter>", on_enter)
        btn_load.bind("<Leave>", on_leave)
        
        btn_save = tk.Button(bb_frame, text="Save", font=font_norm, width=8, command=lambda p=i: save_preset(p))
        btn_save.pack(side=tk.LEFT, padx=2)
        btn_save.bind("<Enter>", on_enter)
        btn_save.bind("<Leave>", on_leave)

    def on_window_close():
        global options_open, options_root
        options_open = False
        options_root = None
        root.destroy()
    
    def on_save():
        global STREAM_URL, HOTKEY_TOGGLE_STREAM, options_open
        global OFFSET_X, OFFSET_Y, MARGIN_RIGHT, MARGIN_BOTTOM
        
        new_url = url_entry.get().strip()
        new_hotkey = hotkey_entry.get().strip()
        
        try:
            new_ox = int(off_x_entry.get())
            new_oy = int(off_y_entry.get())
            new_mr = int(mar_r_entry.get())
            new_mb = int(mar_b_entry.get())
        except ValueError:
            messagebox.showerror("Error", "Margins and positions must be integers.", parent=root)
            return

        if new_url:
            STREAM_URL = new_url
            
        if new_hotkey and new_hotkey != HOTKEY_TOGGLE_STREAM:
            try:
                keyboard.remove_hotkey(HOTKEY_TOGGLE_STREAM)
            except Exception:
                pass
            HOTKEY_TOGGLE_STREAM = new_hotkey
            try:
                keyboard.add_hotkey(HOTKEY_TOGGLE_STREAM, toggle_hide)
            except Exception as e:
                messagebox.showerror("Error", f"Failed to set shortcut {new_hotkey}:\n{e}")
                
        OFFSET_X, OFFSET_Y, MARGIN_RIGHT, MARGIN_BOTTOM = new_ox, new_oy, new_mr, new_mb
                
        # Zapisanie do pliku
        cfg["STREAM_URL"] = STREAM_URL
        cfg["HOTKEY_TOGGLE_STREAM"] = HOTKEY_TOGGLE_STREAM
        cfg["OFFSET_X"] = OFFSET_X
        cfg["OFFSET_Y"] = OFFSET_Y
        cfg["MARGIN_RIGHT"] = MARGIN_RIGHT
        cfg["MARGIN_BOTTOM"] = MARGIN_BOTTOM
        save_config(cfg)
        
        log("Saved options from main window.")
        on_window_close()
        trigger_restart()

    def on_cancel():
        on_window_close()
        
    root.protocol("WM_DELETE_WINDOW", on_window_close)
    
    btn_frame = tk.Frame(root)
    btn_frame.pack(pady=15)
    
    btn_save_main = tk.Button(btn_frame, text="Save and Restart", command=on_save, bg="green", fg="white", width=20, font=("Arial", 10, "bold"))
    btn_save_main.pack(side=tk.LEFT, padx=15)
    btn_save_main.bind("<Enter>", on_enter)
    btn_save_main.bind("<Leave>", on_leave)
    
    btn_cancel = tk.Button(btn_frame, text="Cancel", command=on_cancel, width=15, font=("Arial", 10))
    btn_cancel.pack(side=tk.RIGHT, padx=15)
    btn_cancel.bind("<Enter>", on_enter)
    btn_cancel.bind("<Leave>", on_leave)
    
    # Dopasowanie rozmiarów okna do zawartości i wyśrodkowanie
    root.update_idletasks()
    width = max(580, root.winfo_reqwidth() + 20)
    height = root.winfo_reqheight() + 10
    x = (root.winfo_screenwidth() // 2) - (width // 2)
    y = (root.winfo_screenheight() // 2) - (height // 2)
    root.geometry(f'{width}x{height}+{x}+{y}')
    
    root.deiconify() # Pokazujemy raz od ręki w prawidłowym miejscu
    root.focus_force()
    root.mainloop()

def toggle_options(icon=None, item=None):
    global options_open, options_root
    if options_open and options_root:
        # Pystray działa w innym wątku, więc niszczenie Tk bezpośrednio stąd
        # paraliżuje główną aplikację (deadlock). Zlećmy to wewnątrz samego Tkintera:
        try:
            options_root.after(0, options_root.destroy)
        except Exception:
            pass
        finally:
            options_open = False
            options_root = None
    else:
        # Otwiera okno ustawień w nowym wątku dla pystray
        threading.Thread(target=open_options_dialog, daemon=True).start()

def trigger_quit(icon=None, item=None):
    global quit_requested
    log("!!! QUITTING !!!")
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
    """Generuje skrypt przeglądarki z Niewidzialną Tarczą blokującą klikanie w wideo"""
    flag_escaped = FS_FLAG.replace('\\', '\\\\')
    
    script_content = f'''import webview
import sys
import os
import ctypes
import threading
import time

FS_FLAG = "{flag_escaped}"
PARENT_PID = {os.getpid()}

def is_parent_alive(pid):
    try:
        kernel32 = ctypes.windll.kernel32
        SYNCHRONIZE = 0x00100000
        process_handle = kernel32.OpenProcess(SYNCHRONIZE, False, pid)
        if not process_handle:
            return False
        WAIT_TIMEOUT = 258
        status = kernel32.WaitForSingleObject(process_handle, 0)
        kernel32.CloseHandle(process_handle)
        return status == WAIT_TIMEOUT
    except Exception:
        return True

def parent_watchdog():
    while True:
        if not is_parent_alive(PARENT_PID):
            os._exit(0)
        time.sleep(1)

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
        <iframe src="{STREAM_URL}" allow="autoplay; fullscreen; camera; microphone" allowfullscreen="true" webkitallowfullscreen="true" mozallowfullscreen="true"></iframe>
        
        <!-- Click absorbing layer -->
        <div class="click-shield" oncontextmenu="return false;"></div>
    </div>

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
        // Reaction to native WebView events and fallback polling
        document.addEventListener('fullscreenchange', checkFs);
        setInterval(checkFs, 200);
    </script>
</body>
</html>"""

try:
    threading.Thread(target=parent_watchdog, daemon=True).start()
    api = Api()
    window = webview.create_window("{WINDOW_TITLE}", html=html_content, frameless=True, background_color="#000000", js_api=api, hidden=True)
    webview.start()
except Exception as e:
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
    
    log("=== START CYCLE (WHEP PURE WEBVIEW) ===")
    kill_old_viewers()
    time.sleep(0.5)

    if os.path.exists(FS_FLAG):
        try: os.remove(FS_FLAG)
        except: pass

    log("Searching for Discord...")
    discord_hwnd = find_discord()
    if not discord_hwnd:
        log("ERROR: Discord not found. Waiting 5 seconds...")
        time.sleep(5)
        return 

    log(f"Discord ID: {discord_hwnd}. Generating mini-script player...")
    viewer_script = create_webview_script()
    
    log("Opening stream process...")
    creation_flags = 0
    if os.name == 'nt':
        creation_flags = subprocess.CREATE_NO_WINDOW
    viewer_process = subprocess.Popen(
        [get_launcher_executable(), viewer_script],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=creation_flags
    )

    attempts = 0
    while not viewer_hwnd and attempts < 100:
        time.sleep(0.2)
        if viewer_process.poll() is not None:
            trigger_global_shutdown("Viewer process crashed during startup. Shutting down all app processes.")
            return

        viewer_hwnd = find_viewer_window()
        attempts += 1
    
    if not viewer_hwnd:
        log("ERROR: WebView window did not appear for 20 seconds.")
        viewer_process.terminate()
        trigger_global_shutdown("Viewer window not created in time. Shutting down all app processes.")
        return

    log("Setting window owner to Discord and hiding from taskbar...")
    try:
        ex_style = win32gui.GetWindowLong(viewer_hwnd, win32con.GWL_EXSTYLE)
        new_ex_style = (ex_style | win32con.WS_EX_TOOLWINDOW) & ~win32con.WS_EX_APPWINDOW
        win32gui.SetWindowLong(viewer_hwnd, win32con.GWL_EXSTYLE, new_ex_style)
        
        win32gui.SetWindowLong(viewer_hwnd, win32con.GWL_HWNDPARENT, discord_hwnd)
    except Exception as e:
        log(f"Error during window modification: {e}")
    
    log("Done. Stream is running.")
    while viewer_process.poll() is None:
        if quit_requested or restart_requested:
            if quit_requested: log("Closing stream (Quit)...")
            else: log("Restarting Player process...")
            try: viewer_process.terminate() 
            except: pass
            kill_old_viewers()
            return

        if not is_parent_process_alive(os.getpid()):
            try:
                viewer_process.terminate()
            except Exception:
                pass
            return

        if not win32gui.IsWindow(discord_hwnd):
            log("Discord closed.")
            try: viewer_process.terminate() 
            except: pass
            kill_old_viewers()
            return

        # ========================================
        # SAFE FULLSCREEN LOGIC
        # ========================================
        is_fs_now = os.path.exists(FS_FLAG)
        
        if is_fs_now:
            if not is_currently_fs:
                is_currently_fs = True
                log("Fullscreen detected! Detaching WebView from Discord.")
                
                # Removing owner (Discord) so window can cover full screen independently
                win32gui.SetWindowLong(viewer_hwnd, win32con.GWL_HWNDPARENT, 0)
                
                # Dynamically checking which monitor the app is on
                try:
                    # 2 is the value of win32con.MONITOR_DEFAULTTONEAREST
                    monitor = win32api.MonitorFromWindow(discord_hwnd, 2)
                    monitor_info = win32api.GetMonitorInfo(monitor)
                    m_rect = monitor_info['Monitor']
                    m_x, m_y, m_w, m_h = m_rect[0], m_rect[1], m_rect[2] - m_rect[0], m_rect[3] - m_rect[1]
                except Exception:
                    # Fallback to Primary Monitor
                    m_x, m_y = 0, 0
                    m_w = win32api.GetSystemMetrics(win32con.SM_CXSCREEN)
                    m_h = win32api.GetSystemMetrics(win32con.SM_CYSCREEN)

                # Forcing full screen overlay
                win32gui.SetWindowPos(viewer_hwnd, win32con.HWND_TOPMOST, m_x, m_y, m_w, m_h, win32con.SWP_SHOWWINDOW)
            
            # When fullscreen, 'continue' skips resizing code for Discord
            time.sleep(0.05)
            continue
        else:
            if is_currently_fs:
                is_currently_fs = False
                log("Fullscreen disabled. Reattaching to Discord.")
                # Restoring overlay to Discord window
                win32gui.SetWindowLong(viewer_hwnd, win32con.GWL_HWNDPARENT, discord_hwnd)
                # Removing TOPMOST constraint
                win32gui.SetWindowPos(viewer_hwnd, win32con.HWND_NOTOPMOST, 0, 0, 0, 0, win32con.SWP_NOMOVE | win32con.SWP_NOSIZE)


        # Standard logic for positioning player in Discord
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

    if not quit_requested and not restart_requested:
        trigger_global_shutdown("Viewer process exited unexpectedly. Shutting down all app processes.")
        kill_old_viewers()

def main_loop_thread():
    keyboard.add_hotkey(HOTKEY_TOGGLE_STREAM, toggle_hide)
    
    time.sleep(1)
    
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
    import sys
    suppress_console_output()

    # --- FIX PyInstaller Fork Bomb ---
    # If PyInstaller runs this .exe with webview script argument, execute it and exit!
    if len(sys.argv) > 1 and sys.argv[1].endswith("whep_viewer.py"):
        with open(sys.argv[1], "r", encoding="utf-8") as f:
            code = f.read()
        exec(code)
        sys.exit(0)

    if not ensure_single_instance():
        sys.exit(0)

    t = threading.Thread(target=main_loop_thread)
    t.daemon = True
    t.start()

    menu = pystray.Menu(
        pystray.MenuItem("Close/Open Settings", toggle_options),
        pystray.MenuItem("Restart Stream", trigger_restart),
        pystray.MenuItem("Quit", trigger_quit)
    )

    icon = pystray.Icon("DiscordStream", create_tray_icon(), "Discord Stream Overlay", menu)
    tray_icon = icon
    
    try:
        icon.run()
    except Exception as e:
        trigger_global_shutdown("Tray icon crashed. Shutting down all app processes.")