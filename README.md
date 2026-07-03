# 📺 WhipCast

A lightweight and intuitive tool that allows you to watch streams with ultra-low latency (**< 100ms**).

The application allows you to watch the video stream in a clean, borderless window on Windows (10/11) systems with an optional feature to "attach" it directly to other applications like Discord, or display it in a dedicated app mode on Linux systems. The video stream is fetched directly from a streamer using OBS and the WHIP (WebRTC) / HLS protocol.

---

## ✨ Key Features
- **Ultra-low latency** (<100ms) thanks to modern protocol support.
- **Windows**: A borderless, floating window with an optional intelligent overlay mode that can attach directly to other applications like Discord.
- **Linux**: A dedicated app mode (`--app`) powered by the Chromium engine (clean window, no browser interface).
- **Optimized interface**: Isolated browser profiles and a built-in "click-shield" to prevent accidental video pausing.

---

## 🌐 Network Requirements (Important!)
The application relies on a direct connection (P2P / self-hosted) between the streamer's server and the viewer. For the tool to work over the internet, you must have direct network visibility (VPN or port forwarding).

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

In order for viewers to watch your stream with ultra-low latency, you need to provide them with a preview via an appropriate server.

The operation of the tool has been **officially confirmed with the MediaMTX server**. However, the tool is universal and should work with other solutions, such as:
- ✅ **MediaMTX** (recommended, built-in WebRTC/WHIP support)
- **OBS WebRTC**
- **Nginx-RTMP** (with HLS output - higher latency)

### How to configure streaming in OBS Studio (WHIP)

If you are using a server like MediaMTX, configuring OBS Studio is incredibly simple and requires no additional plugins.

1. Ensure your server (e.g., MediaMTX) is running and ready to accept connections.
2. Open **OBS Studio** and go to **Settings**.
3. Navigate to the **Stream** tab.
4. From the **Service** dropdown list, select **WHIP**.
5. In the **Server** field, paste the publishing URL. This address usually needs to contain a parameter to reduce server buffering to a minimum. It should look something like this:
   
   `http://192.168.x.x:8889/stream/whip?buffer=0`
   *(Change the IP address to your virtual VPN address, public, or local address, depending on your chosen connection method).*

6. You can leave the **Bearer Token** field empty.
7. Click **Apply** and **Start Streaming**.

### Output Configuration in OBS

For everything to run smoothly and without errors in the viewers' browsers, avoid overcomplicating things with unusual encoders. Go to the **Output** -> **Streaming** tab and ensure you have the following parameters set:
* **Video Encoder:** A **hardware H.264 encoder** is recommended (e.g., NVIDIA NVENC H.264 or AMD HW H.264). You can use CPU encoding (x264), but it involves a significant CPU load and worse performance.
* **Audio Encoder:** Select **FFmpeg Opus**.
* **Rate Control:** Set to **CBR** (Constant Bitrate).

Example configuration confirmed to work:

![OBS Output Configuration](insert-link-to-your-screenshot-here.png)

### Viewing Link (for the viewer)
Remember that you must give your viewers the **viewing link** (WebRTC/WHEP), not the broadcasting link (WHIP). If you send the stream in OBS to an address ending with `/whip`, the viewer should enter the main address in the WhipCast app, e.g.:
`http://192.168.x.x:8889/stream`
