# Discord Stream Overlay

This tool allows you to watch streams directly over your Discord window. It behaves like a native overlay, replacing the standard Discord stream view with a high-performance web-based player that "sticks" to your Discord window.

### 🚀 Features
*   **Webview-based Player**: Directly load stream URLs (HTTP, WebRTC, HLS, etc.) without needing external players like MPV.
*   **System Tray Integration**: Runs quietly in the background with a convenient system tray menu.
*   **GUI Settings**: Manage your stream URL, hotkeys, and overlay margins easily from an intuitive UI—no script editing required.
*   **Margin Presets**: Save and load up to 3 custom layout presets to quickly adjust to different Discord server views.
*   **Seamless Overlay**: The video window automatically attaches to Discord. If you move or resize Discord, the stream follows perfectly.
*   **Global Hotkey Toggle**: Easily hide or show the stream overlay with a quick key combination (default: `F7 + F8`).
*   **Standalone Executable Support**: Can be compiled into a single `.exe` using PyInstaller for zero-setup execution.

---

## 🛠️ Prerequisites

If you plan to run the app from the source code:
1.  **Windows 10 or 11**.
2.  **Python 3.x** installed ([Download here](https://www.python.org/downloads/)).
3.  **Git** (optional, to clone the repo).

*(If you are using a pre-compiled `.exe` release, you don't need Python installed!)*

---

## 📥 Installation & Setup

### Option 1: Running from Source
1. Clone this repository or download it as a ZIP and extract it.
2. Open a terminal (Command Prompt/PowerShell) in the project folder and run:
   `ash
   pip install -r requirements.txt
   `
3. Run the application:
   `ash
   python embed_discord.py
   `

### Option 2: Building an Executable
To create a standalone `.exe` file that doesn't require Python:
1. Ensure dependencies are installed (`pip install -r requirements.txt`).
2. Run the provided PyInstaller command (or execute it from `pyinstaller.txt`):
   `ash
   pyinstaller --noconfirm --onefile --windowed --icon="assets/icon.ico" --name "Discord_Stream_Overlay" embed_discord.py
   `
3. Your compiled app will be located in the `dist/` folder.

---

## ⚙️ Configuration

No need to edit python files anymore! All configuration is handled through the Graphical User Interface.

1.  **Start the app**. You will see a new icon appear in your Windows System Tray (bottom right corner).
2.  **Right-click the tray icon** and select **"Options"**.
3.  **Stream URL**: Enter the web player or stream URL provided by your friend (e.g., `http://192.168.8.122:8889/stream`).
4.  **Offsets & Margins**: Adjust the values so the stream doesn't overlap with your Discord servers, friend list, or chat.
    *   `Offset X`: Space from the left edge of Discord.
    *   `Offset Y`: Space from the top edge.
    *   `Margin Right`: Space pushing in from the right edge.
    *   `Margin Bottom`: Space pushing up from the bottom edge.
5.  **Presets**: Use the `Load 1/2/3` and `Save 1/2/3` buttons to quick-swap between margin setups.
6.  **Hotkey**: Set a custom keybind (e.g. `f7+f8`) to hide/show the overlay instantly.
7.  Click **Save**. The overlay will restart and apply your settings.

*(Configuration is safely stored in `%APPDATA%\Discord_Stream_Overlay\config.json`)*

---

## 🎥 OBS Settings (For the Streamer)

Since the app now uses a Webview element, the streamer needs to host a web-compatible stream. Common solutions include:
*   **MediaMTX**: To serve WebRTC / HLS streams directly.
*   **OBS-WebRTC Plugin**: For ultra-low latency streaming directly from OBS to a browser source.
*   **Nginx-RTMP**: Pushing OBS to a local server that provides an HLS output.

Once the streaming server is set up, the streamer will provide you with an `http://...` URL that you can simply paste into your **Options** menu.

---

## 🎮 How to Use

1.  **Open Discord**.
2.  Start the **Discord Stream Overlay** app.
3.  The stream window will launch, automatically detect your Discord window, and snap to its layout.
4.  Use your **Toggle Hotkey** (default `F7 + F8`) if you need to quickly hide the stream to interact with Discord underneath.
5.  To close the application entirely, right-click the System Tray icon and select **Quit**.
