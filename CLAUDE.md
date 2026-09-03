# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

WhipCast is a low-latency stream viewer. The streamer publishes via WHIP/WebRTC
(typically MediaMTX); WhipCast just wraps the server's own WHEP player page in a
clean, chrome-less window.

There is **no shared code between platforms** — two independent implementations:

| Platform | Entry point | Stack |
|---|---|---|
| Windows | `WhipCast/` (`.NET 9`, `net9.0-windows`) | WinForms + WebView2 |
| Linux | `embed_linux.py` (single file) | Python, shells out to a Chromium browser in `--app` mode |

The Linux side does not import anything from `WhipCast/`, and vice versa. A change
on one side is almost never mirrored automatically on the other.

## Build & run

No test suite exists.

SDKs **8.0.424** and **9.0.317** are installed and `dotnet` is on `PATH`
(`C:\Program Files\dotnet`). The project targets `net9.0-windows`, so SDK 9 is the one
that matters. If a long-lived shell reports `dotnet` as unknown, it inherited a stale
environment block from before the SDK install — open a new shell, or fall back to
`& "$env:ProgramFiles\dotnet\dotnet.exe" …`.

A clean `Release` build currently emits **0 errors and 32 warnings**. Those warnings
are pre-existing baseline noise — nullability (`CS8618`/`CS8622`/`CS8625`/`CS860x`,
the codebase enables `<Nullable>enable</Nullable>` but was not written for it) plus
`MSB3277` about a `WindowsBase` version unification pulled in by the WebView2 package's
unused WPF assembly. Judge a change by whether it *adds* warnings, not by the total.

Releases are produced by [`.github/workflows/build.yml`](.github/workflows/build.yml)
(manual `workflow_dispatch`, takes a `vX.Y.Z` version input, builds both targets and
attaches them to a GitHub Release).

```bash
dotnet build WhipCast/WhipCast.csproj -c Release
```

```bash
dotnet run --project WhipCast/WhipCast.csproj
```

Release build as CI does it (self-contained single-file exe):

```bash
dotnet publish WhipCast/WhipCast.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Linux:

```bash
python embed_linux.py http://192.168.x.x:8889/stream 1280 720
```

`requirements.txt` covers both the Linux runtime and the PyInstaller build; most
entries are `sys_platform == 'win32'`-gated leftovers from an earlier pywebview
implementation and are unused by `embed_linux.py`.

## Windows architecture

`Program.cs` → single-instance `Global\` mutex → `Application.Run(new AppContext())`.

- **`AppContext.cs`** — the orchestrator. Owns the config, both global hotkeys, the
  overlay form, and a `Timer` ticking at the *fastest monitor's refresh interval*
  (`WindowManager.GetFastestMonitorRefreshRate()`), which is what keeps the attached
  overlay glued to the Discord window without visible lag.
- **`OverlayForm.cs`** — a `Form` hosting a single `WebView2`. All UI lives in HTML.
- **`WindowManager.cs`** — nothing but P/Invoke declarations plus `FindTargetWindow()`.
- **`Config.cs`** — `AppConfig` POCO + JSON load/save. Both `Load` and `Save` swallow
  all exceptions and fall back to defaults.
- **`GlobalHotkey.cs`** — low-level keyboard hook (`WH_KEYBOARD_LL`). Two separate
  instances are created: one for stream visibility, one for mode toggle.
- **`ToggleSwitch.cs`** — a custom-painted WinForms control that is **currently unused**
  (the settings UI moved to HTML).

### The two run modes

`AppConfig.ATTACH_TO_WINDOW` selects between them, and they behave very differently:

- **Standalone (`false`)** — an ordinary sizable 1280×720 window, in the taskbar, with
  the app icon and immersive dark title bar.
- **Attached (`true`)** — a borderless `WS_EX_TOOLWINDOW` form *owned* by the Discord
  hwnd (`Show(new WindowWrapper(targetHwnd))`), hidden from the taskbar, and
  repositioned on every timer tick to `discordRect + OFFSET_* - MARGIN_*`.
  `FindTargetWindow()` locates Discord by enumerating visible windows whose title is
  `"Discord"` or ends with `" - Discord"`, and requires width > 200 px to skip Discord's
  transient mini-windows.

**The repositioning tick is dirty-checked** against `lastX/lastY/lastW/lastH`: it calls
`SetWindowPos` only when Discord's rect actually differs from the cached one. So any
code that moves or resizes the overlay behind the timer's back — fullscreen being the
obvious case — **must reset that cache to `-1`**, or the timer will see an unchanged
Discord rect, skip the update, and leave the window wherever it was put.

**Standalone is the default for fresh installs** — `AppConfig.ATTACH_TO_WINDOW` defaults
to `false`, so a user with no `config.json` yet gets a normal window rather than an
overlay glued to Discord. This is only a default: once a config file exists, the saved
value wins on every launch. Users switch modes with the menu toggle or the mode hotkey
(default `F8+F9`), and both persist the choice.

### Mode switching requires a new form

`OverlayForm.CreateParams` reads `currentConfig.ATTACH_TO_WINDOW` to decide on
`WS_EX_TOOLWINDOW`, and extended styles are fixed at window-creation time. So switching
modes means **disposing and recreating** the form — that is what `StartStreamCycle()`
does. Do not try to mutate an existing form into the other mode.

### Restart flow

Never restart from inside an event handler (you would be disposing the form that is
currently dispatching). Instead:

1. Handler sets `restartRequested = true` (`TriggerRestart`).
2. `LoopTimer_Tick` notices it and calls `StartStreamCycle()`.
3. `StartStreamCycle` sets `overlayForm.IsRestarting = true` before `Close()`, so
   `OnFormClosing` does not mistake the teardown for a user-initiated exit and fire
   `RequestExit`.

`StartStreamCycle` may legitimately return without creating a form (Discord not found,
or minimized/hidden) — the timer retries on later ticks.

### C# ↔ HTML bridge

`WhipCast/menu.html` is an **embedded resource** (`LogicalName="WhipCast.menu.html"`,
see the `.csproj`), read at runtime, with `__STREAM_URL__` replaced by string
substitution and handed to `NavigateToString`. It is *not* loaded from disk — editing
it requires a rebuild.

The page contains three layers: an `<iframe>` with the actual stream, an invisible
"click-shield" (`height: calc(100% - 52px)`) that blocks accidental pause clicks while
leaving the player's bottom control bar usable, and the settings menu overlay.

Messages **HTML → C#** (`window.chrome.webview.postMessage`), handled in
`CoreWebView2_WebMessageReceived`:

| Message | Effect |
|---|---|
| `{type:'REQUEST_CONFIG'}` | C# replies with `{type:'LOAD_CONFIG', config}` |
| `{type:'SAVE_AND_RESTART', config}` | parse → save → restart |
| `{type:'SAVE_PRESET', presetId, config}` | write one of presets `"1"`/`"2"`/`"3"` |
| `{type:'LOAD_PRESET', presetId}` | apply preset → save → restart |
| `{type:'EXIT_APP'}` | quit |
| `'FS_ON'` / `'FS_OFF'` (raw strings, not JSON) | fullscreen enter/leave |

Fullscreen is handled differently per mode: standalone borderless-maximizes the form;
attached mode detaches the owner (`SetWindowLongPtr(GWL_HWNDPARENT, 0)`) and topmosts
the window over Discord's monitor, then re-parents on exit.

A script injected via `AddScriptToExecuteOnDocumentCreatedAsync` runs *inside the
iframe*, watches the DOM for the server's error text ("stream not found", "peer
connection closed", …) and for a `<video>` reaching `playing`, and reports
`connecting` / `live` / `offline` up to the parent page, which swaps the overlay
screens. Server-side wording changes will silently break offline detection.

### Adding a config field

A new setting must be touched in **four** places or it will silently not round-trip:

1. `Config.cs` — property on `AppConfig` (or `Preset`).
2. `OverlayForm.ParseConfigElement` — explicit `TryGetProperty` read; fields missing
   here are dropped on every save.
3. `menu.html` `populateForm()` — config → DOM.
4. `menu.html` `gatherForm()` — DOM → config.

## Config file locations

- Windows: `%APPDATA%\whip-cast\config.json`
- Linux: `~/.config/whip-cast_stream_overlay/config.json` — note this does **not**
  match the path claimed in the README (`~/.config/whip-cast`), and the two platforms
  use different config schemas (`STREAM_URL` is the only shared key).

WebView2 keeps its profile in `%APPDATA%\whip-cast\WebView2Data`; the Linux side
creates its own isolated Chromium profile so the user's extensions cannot break the
player.

## Gotchas

- **`WhipCast/extract_html.py` is a spent one-shot migration script.** It carved
  `menu.html` out of a former inline string in `OverlayForm.cs` and rewrote that file.
  The patterns it greps for no longer exist. Do not run it.
- Default `STREAM_URL` is a hard-coded LAN address (`http://192.168.8.122:8889/stream`)
  in both `Config.cs` and `embed_linux.py`.
- Config defaults for `OFFSET_*` / `MARGIN_*` are tuned for Discord's chrome and are
  meaningless in standalone mode.
- `.gitignore` ignores `TODO*`.
- Commit history and code comments are English; CI workflow input descriptions are Polish.
