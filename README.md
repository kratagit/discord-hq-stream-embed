# 📺 WhipCast

A lightweight and intuitive tool that allows you to watch streams with ultra-low latency (**< 100ms**).

The application allows you to "stick" the video preview directly to the Discord window on Windows (10/11) systems or display it in a dedicated, clean window on Linux systems. The video stream is fetched directly from a streamer using OBS and the WHIP (WebRTC) / HLS protocol.

---

## ✨ Key Features
- **Ultra-low latency** (<100ms) thanks to modern protocol support.
- **Windows**: An intelligent overlay rendered directly over the Discord window.
- **Linux**: A dedicated app mode (`--app`) powered by the Chromium engine (clean window, no browser interface).
- **Optimized interface**: Isolated browser profiles and a built-in "click-shield" to prevent accidental video pausing.

---

## 🚀 Quick Start (for viewers)

### 🖥️ Windows (10 / 11)

1. Download the latest `.exe` version from the **[Releases](../../releases)** tab.
2. Run the downloaded `WhipCast-...-Windows-x64.exe` file.
3. Open the Discord application.
4. Hover over the top-left corner of the stream overlay to reveal the hamburger menu (≡) and click it to open settings.
5. Paste the link received from the streamer into the **Stream URL** field and click **Save and restart stream**.
6. When the streamer starts broadcasting, the video will automatically appear on your Discord!

> **Note:** On the first launch, the Windows SmartScreen filter might block the application. You need to click *"More info"* -> *"Run anyway"*.

### 🐧 Linux

The Linux version runs as a standalone, minimalist web application using the Chromium engine.

1. Download the Linux executable (e.g., `WhipCast-...-Linux-x86_64`) from the **[Releases](../../releases)** tab.
2. Grant it execution permissions: `chmod +x WhipCast-*-Linux-x86_64`.
3. Configure or start the stream directly from the terminal:

```bash
# Launch with a specific link and window size
./WhipCast-1.0.0-Linux-x86_64 http://stream-link/stream 1280 720

# Save configuration only (without launching)
./WhipCast-1.0.0-Linux-x86_64 http://stream-link/stream --save-only
```
4. For subsequent launches, simply double-click the file (or run it without arguments) – the application will remember your last settings.

---

## 🛠️ Configuration and Usage

### Windows (In-App Menu)
- **Stream URL** - The network address of the stream.
- **Attach to window** - Toggle attaching the overlay to the Discord window.
- **Toggle Stream Key** - Keyboard shortcut to instantly show/hide the overlay (default `F9`).
- **Toggle Mode Key** - Keyboard shortcut to switch between window sizes/modes (default `F8+F9`).
- **Offset X / Offset Y** - Precise image offset from the left/top edge of the Discord window.
- **Margin Right / Margin Bottom** - Margins that determine the video size.
- **Presets 1 / 2 / 3** - Buttons for quickly switching between saved settings profiles.

*After clicking the **Save and restart stream** button, the overlay will automatically restart with the new parameters.*

### Linux (Technical Details and CLI)
The Linux version features several advanced mechanisms under the hood:
- **Requirements:** Any Chromium-based browser (Chrome, Chromium, Brave, Edge, Vivaldi) is required to run. If none is found, the program will fall back to opening the link in the system's default browser.
- **Isolated profile:** The application creates its own browser profile. This ensures that your plugins (e.g., adblockers, cashback extensions) do not interfere with the stream or break the window.
- **Click-Shield:** The generated player has an invisible protective layer on top. It blocks accidental clicks (and pausing) in the middle of the video, but leaves 52 pixels free at the bottom of the screen, allowing you to freely use the volume bar or fullscreen mode.

**Available CLI parameters (Linux):**
Syntax: `[URL] [Width][Height] [--save-only]`

You can mix them freely, e.g.:
- Change only the window size: `./app 1920 1080`
- Change only the link and save: `./app http://new-link --save-only`

### 📁 Where is the configuration saved?
On both Windows and Linux, your settings are saved in the `config.json` file:
- **Windows:** `%APPDATA%\whip-cast\config.json`
- **Linux:** `~/.config/whip-cast/config.json`

---

## 📡 Streamer Requirements

For this tool to work, the broadcaster (streamer) must generate and share a web stream link (WebRTC or HLS) with you. The tool is compatible with solutions such as:
- **MediaMTX**
- **OBS WebRTC**
- **Nginx-RTMP** (with HLS output)

As a viewer, you only need the URL received from the streamer (e.g., `http://192.168.x.x:8889/stream`).