# Discord Low-Latency Stream Overlay (MPV + SRT)

This tool allows you to watch your friend's OBS stream with **near-zero latency** (SRT protocol) directly over your Discord window. It replaces the standard Discord stream view with a high-performance **MPV player** that "sticks" to your Discord window, behaving like a native overlay.

### 🚀 Features
*   **Ultra Low Latency:** Watch streams with <50ms delay (SRT Protocol).
*   **Seamless Overlay:** The video window attaches to Discord. If you move or minimize Discord, the stream follows.
*   **Dynamic Resizing:** The video automatically adjusts its size when you resize Discord.
*   **Advanced Stats:** Built-in HUD showing Latency (ms), FPS, Bitrate, and Drops (toggle with `TAB`).
*   **Zero Bloat:** Minimal resource usage compared to browser-based streaming.

---

## 🛠️ Prerequisites

Before you start, make sure you have:
1.  **Windows 10 or 11**.
2.  **Python 3.x** installed ([Download here](https://www.python.org/downloads/)).
3.  **Git** (optional, to clone the repo).

---

## 📥 Installation

### 1. Download the Project
Clone this repository or download it as a ZIP file and extract it to a folder (e.g., `Documents/Discord-Overlay`).

### 2. Install Python Dependencies
Open a terminal (Command Prompt) in the project folder and run:
```bash
pip install -r requirements.txt
```

### 3. Download MPV Player
Since MPV is not included in this repo (to keep it lightweight), you must download it separately:
1.  Go to [MPV Windows Builds (by shinchiro)](https://sourceforge.net/projects/mpv-player-windows/files/64bit-v3/).
2.  Download the latest version (e.g., `mpv-x86_64-v3-...-git-...`).
3.  Extract the files to a permanent location on your PC (e.g., `C:\Apps\mpv`).
4.  **Note down the path** where `mpv.exe` is located.

---

## ⚙️ Configuration

You need to tell the script where your `mpv.exe` is and what IP address to listen to.

1.  Open `embed_discord.py` in a text editor (Notepad, VS Code).
2.  Find the **CONFIGURATION** section at the top.

### Step 1: Set MPV Path
Change the `MPV_EXE` variable to your actual path. Use `r` before the quotes!
```python
# Example:
MPV_EXE = r"C:\Apps\mpv\mpv.exe"
```

### Step 2: Set Stream Source
Change the `SOURCE` variable in the `else` block (around line 50) to match your friend's IP and Port.
```python
# Replace 0.0.0.0 with your local IP or leave it if you are listening on all interfaces
SOURCE = "srt://0.0.0.0:10000?mode=listener&latency=20000"
```

### Step 3: Adjust Margins (Optional)
If the overlay covers your friend list or server list, adjust these values in the script:
```python
OFFSET_X = 325      # Space from the left
OFFSET_Y = 38       # Space from the top
MARGIN_RIGHT = 8    # Space from the right
MARGIN_BOTTOM = 66  # Space from the bottom
```

---

## 🎥 OBS Settings (For the Streamer)

For this to work with **zero latency**, your friend (the streamer) must configure OBS correctly.

1.  **Output Mode:** Advanced.
2.  **Encoder:** NVIDIA NVENC H.264 (new).
3.  **Rate Control:** CBR.
4.  **Bitrate:** 6000 - 10000 Kbps.
5.  **Preset:** P1 (Fastest) or P2 (Faster).
6.  **Tuning:** Ultra Low Latency.
7.  **Keyframe Interval:** 1 s.
8.  **Max B-frames:** **0** (CRITICAL! Set this to 0 to remove delay).

**Stream URL in OBS:**
Instead of selecting a service like Twitch, choose **Custom...**:
*   **Server:** `srt://YOUR_PUBLIC_IP:10000?mode=caller&latency=20000`
*(Replace `YOUR_PUBLIC_IP` with your external IP address. You might need to port forward port 10000 UDP on your router).*

---

## 🎮 How to Use

1.  **Open Discord** first.
2.  Run the script:
    ```bash
    python embed_discord.py
    ```
3.  A black window will appear and snap to Discord. It starts muted by default, so you need to unmute it manually when you want audio.
4.  Ask your friend to **Start Streaming** in OBS.
5.  The video should appear instantly.

### Hotkeys
| Key | Action |
| :--- | :--- |
| **F7 + F8** | Toggle the stream visibility (Hide/Show) |
| **TAB** | Toggle Stats (Latency, FPS, Bitrate, Drops) |
| **Ctrl + C** | Stop the script and close MPV (in console) |

---

## ❓ Troubleshooting

**"The window appears for a second and closes"**
*   Check if the path to `mpv.exe` in `embed_discord.py` is correct.
*   Make sure `input.conf` and the `scripts` folder are in the same directory as the python script.

**"I see a black screen"**
*   The script is running and waiting for data. Check if your friend is streaming to the correct IP/Port.
*   Check your Windows Firewall (allow UDP port 10000).

**"The FPS is 0"**
*   This is normal for some SRT streams. As long as the video is smooth, ignore it. The script tries to estimate FPS automatically.

---

### License
Open Source. Feel free to modify and share!
