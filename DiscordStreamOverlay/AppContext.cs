using System;
using System.Drawing;
using System.Windows.Forms;

namespace DiscordStreamOverlay
{
    public class AppContext : ApplicationContext
    {
        private AppConfig config;
        private GlobalHotkey globalHotkey;
        private OverlayForm overlayForm;
        private System.Windows.Forms.Timer loopTimer;

        private IntPtr discordHwnd = IntPtr.Zero;
        private bool isVisible = true;
        private bool restartRequested = false;
        
        private int lastX = -1, lastY = -1, lastW = -1, lastH = -1;

        private class WindowWrapper : IWin32Window
        {
            public IntPtr Handle { get; }
            public WindowWrapper(IntPtr handle) { Handle = handle; }
        }

        public AppContext()
        {
            config = ConfigManager.Load();

            Icon standaloneIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            globalHotkey = new GlobalHotkey();
            globalHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_STREAM);
            globalHotkey.HotkeyPressed += GlobalHotkey_HotkeyPressed;

            // settingsForm is removed

            StartStreamCycle();

            int maxHz = WindowManager.GetFastestMonitorRefreshRate();
            int intervalMs = Math.Max(1, 1000 / maxHz);

            loopTimer = new System.Windows.Forms.Timer { Interval = intervalMs }; // e.g., 6ms for 144Hz, 16ms for 60Hz
            loopTimer.Tick += LoopTimer_Tick;
            loopTimer.Start();
        }

        private void StartStreamCycle()
        {
            if (overlayForm == null || overlayForm.IsDisposed)
            {
                Icon standaloneIcon = null;
                try
                {
                    using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("DiscordStreamOverlay.icon_s.ico"))
                    {
                        if (stream != null) standaloneIcon = new Icon(stream);
                        else
                        {
                            string exeDir = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? Application.StartupPath) ?? Application.StartupPath;
                            string iconPath = System.IO.Path.Combine(exeDir, "assets", "icon_s.ico");
                            if (System.IO.File.Exists(iconPath)) standaloneIcon = new Icon(iconPath);
                        }
                    }
                }
                catch { }

                string windowTitle = $"Discord Stream Overlay - {config.STREAM_URL}";
                overlayForm = new OverlayForm(config, true, windowTitle, standaloneIcon);
                WireUpOverlayFormEvents();
                
                // Set initial mode state
                ApplyCurrentMode();
                overlayForm.Show();
            }
            else
            {
                // Just apply mode if already exists
                ApplyCurrentMode();
            }
        }

        private void ApplyCurrentMode()
        {
            if (overlayForm == null || overlayForm.IsDisposed) return;
            if (overlayForm.IsFullscreenMode) return; // Don't mess with window if fullscreen

            if (!config.ATTACH_TO_DISCORD)
            {
                overlayForm.FormBorderStyle = FormBorderStyle.Sizable;
                
                IntPtr viewerHwnd = overlayForm.Handle;

                // Show borders and apply frame changes
                WindowManager.SetWindowPos(viewerHwnd, WindowManager.HWND_NOTOPMOST, 0, 0, 0, 0, 
                    WindowManager.SWP_NOMOVE | WindowManager.SWP_NOSIZE | WindowManager.SWP_SHOWWINDOW | 0x0020 /* SWP_FRAMECHANGED */);
            }
            else
            {
                discordHwnd = WindowManager.FindDiscordWindow();
                if (discordHwnd != IntPtr.Zero)
                {
                    overlayForm.FormBorderStyle = FormBorderStyle.None;
                    
                    IntPtr viewerHwnd = overlayForm.Handle;

                    // Apply frame changes
                    WindowManager.SetWindowPos(viewerHwnd, IntPtr.Zero, 0, 0, 0, 0, 
                        WindowManager.SWP_NOMOVE | WindowManager.SWP_NOSIZE | WindowManager.SWP_NOZORDER | 0x0020 /* SWP_FRAMECHANGED */);
                    
                    // Force position update in the next loop tick
                    lastX = -1; lastY = -1; lastW = -1; lastH = -1;
                }
            }
        }

        private void WireUpOverlayFormEvents()
        {
            overlayForm.FullscreenStateChanged += OverlayForm_FullscreenStateChanged;
            overlayForm.SettingsSaved += OverlayForm_SettingsSaved;
            overlayForm.PresetSaved += OverlayForm_PresetSaved;
            overlayForm.PresetLoaded += OverlayForm_PresetLoaded;
            overlayForm.ExitRequested += (s, e) => { ExitApplication(); };
            
            overlayForm.FormClosed += (s, e) => 
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    ExitApplication();
                }
            };
        }

        private void ExitApplication()
        {
            if (loopTimer != null) 
            {
                loopTimer.Stop();
                loopTimer.Tick -= LoopTimer_Tick;
            }
            if (globalHotkey != null)
            {
                globalHotkey.Dispose();
            }
            ExitThread();
        }

        private void OverlayForm_SettingsSaved(object sender, AppConfig newConfig)
        {
            bool urlChanged = config.STREAM_URL != newConfig.STREAM_URL;
            config = newConfig;
            ConfigManager.Save(config);
            globalHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_STREAM);

            if (urlChanged)
            {
                if (overlayForm != null && !overlayForm.IsDisposed)
                {
                    overlayForm.Close();
                    overlayForm = null;
                }
            }

            TriggerRestart(null, null);
        }

        private void OverlayForm_PresetSaved(object sender, int presetId)
        {
            string pId = presetId.ToString();
            if (!config.PRESETS.ContainsKey(pId)) config.PRESETS[pId] = new Preset();
            config.PRESETS[pId].OFFSET_X = config.OFFSET_X;
            config.PRESETS[pId].OFFSET_Y = config.OFFSET_Y;
            config.PRESETS[pId].MARGIN_RIGHT = config.MARGIN_RIGHT;
            config.PRESETS[pId].MARGIN_BOTTOM = config.MARGIN_BOTTOM;
            ConfigManager.Save(config);
        }

        private void OverlayForm_PresetLoaded(object sender, int presetId)
        {
            string pId = presetId.ToString();
            if (config.PRESETS.ContainsKey(pId))
            {
                var p = config.PRESETS[pId];
                config.OFFSET_X = p.OFFSET_X;
                config.OFFSET_Y = p.OFFSET_Y;
                config.MARGIN_RIGHT = p.MARGIN_RIGHT;
                config.MARGIN_BOTTOM = p.MARGIN_BOTTOM;
                ConfigManager.Save(config);
                TriggerRestart(null, null);
            }
        }

        private FormBorderStyle previousBorderStyle = FormBorderStyle.Sizable;
        private FormWindowState previousWindowState = FormWindowState.Normal;

        private void OverlayForm_FullscreenStateChanged(object sender, EventArgs e)
        {
            if (overlayForm == null) return;
            IntPtr viewerHwnd = overlayForm.Handle;

            if (!config.ATTACH_TO_DISCORD)
            {
                if (overlayForm.InvokeRequired)
                {
                    overlayForm.Invoke(new Action(() => OverlayForm_FullscreenStateChanged(sender, e)));
                    return;
                }

                if (overlayForm.IsFullscreenMode)
                {
                    previousBorderStyle = overlayForm.FormBorderStyle;
                    previousWindowState = overlayForm.WindowState;
                    
                    overlayForm.SuspendLayout();
                    overlayForm.FormBorderStyle = FormBorderStyle.None;
                    if (overlayForm.WindowState == FormWindowState.Maximized)
                        overlayForm.WindowState = FormWindowState.Normal; // Force refresh
                    overlayForm.WindowState = FormWindowState.Maximized;
                    overlayForm.TopMost = true;
                    overlayForm.ResumeLayout();
                }
                else
                {
                    overlayForm.SuspendLayout();
                    overlayForm.FormBorderStyle = previousBorderStyle;
                    overlayForm.WindowState = previousWindowState;
                    overlayForm.TopMost = false;
                    overlayForm.ResumeLayout();
                }
                return;
            }

            if (overlayForm.IsFullscreenMode)
            {
                // Find nearest monitor to Discord
                IntPtr monitor = WindowManager.MonitorFromWindow(discordHwnd, WindowManager.MONITOR_DEFAULTTONEAREST);
                WindowManager.MONITORINFO monitorInfo = new WindowManager.MONITORINFO();
                monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(WindowManager.MONITORINFO));
                
                int mx = 0, my = 0, mw = Screen.PrimaryScreen.Bounds.Width, mh = Screen.PrimaryScreen.Bounds.Height;
                if (WindowManager.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    mx = monitorInfo.rcMonitor.Left;
                    my = monitorInfo.rcMonitor.Top;
                    mw = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                    mh = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                }

                WindowManager.SetWindowPos(viewerHwnd, WindowManager.HWND_TOPMOST, mx, my, mw, mh, WindowManager.SWP_SHOWWINDOW);
            }
            else
            {
                // Reattach
                WindowManager.SetWindowPos(viewerHwnd, WindowManager.HWND_NOTOPMOST, 0, 0, 0, 0, WindowManager.SWP_NOMOVE | WindowManager.SWP_NOSIZE);
            }
        }

        private void LoopTimer_Tick(object sender, EventArgs e)
        {
            if (restartRequested)
            {
                restartRequested = false;
                StartStreamCycle();
                return;
            }

            if (!config.ATTACH_TO_DISCORD)
            {
                return; // In standalone mode, do not track Discord
            }

            if (!WindowManager.IsWindow(discordHwnd))
            {
                // Discord closed, find again
                discordHwnd = WindowManager.FindDiscordWindow();
                if (discordHwnd == IntPtr.Zero && overlayForm != null)
                {
                    overlayForm.Close();
                    overlayForm = null;
                }
                return;
            }

            // If Discord is valid but overlay is missing, try to create it IF Discord is ready
            if (overlayForm == null || overlayForm.IsDisposed)
            {
                if (!WindowManager.IsIconic(discordHwnd) && WindowManager.IsWindowVisible(discordHwnd))
                {
                    StartStreamCycle();
                }
                return; // Wait until created
            }

            if (overlayForm == null || overlayForm.IsDisposed) return;
            if (overlayForm.IsFullscreenMode) return; // Skip resizing if fullscreen

            IntPtr viewerHwnd = overlayForm.Handle;

            if (WindowManager.IsIconic(discordHwnd))
            {
                // When Discord is minimized to taskbar, the OS automatically hides owned windows.
                // We do NOT explicitly call SW_HIDE because it breaks the OS restore logic.
            }
            else if (!WindowManager.IsWindowVisible(discordHwnd))
            {
                // Discord is hidden to the system tray (not minimized). The OS doesn't hide the child.
                // We must hide it manually.
                if (WindowManager.IsWindowVisible(viewerHwnd))
                {
                    WindowManager.ShowWindow(viewerHwnd, WindowManager.SW_HIDE);
                }
                
                // Invalidate cache so when Discord comes back, it forces SWP_SHOWWINDOW
                lastX = -1; lastY = -1; lastW = -1; lastH = -1;
            }
            else
            {
                if (isVisible)
                {
                    WindowManager.GetWindowRect(discordHwnd, out WindowManager.RECT rect);
                    int d_x = rect.Left;
                    int d_y = rect.Top;
                    int d_w = rect.Right - rect.Left;
                    int d_h = rect.Bottom - rect.Top;

                    if (d_x != lastX || d_y != lastY || d_w != lastW || d_h != lastH)
                    {
                        int new_width = d_w - config.OFFSET_X - config.MARGIN_RIGHT;
                        int new_height = d_h - config.OFFSET_Y - config.MARGIN_BOTTOM;

                        if (new_width < 100) new_width = 100;
                        if (new_height < 100) new_height = 100;

                        if (new_width > 0 && new_height > 0)
                        {
                            IntPtr insertAfter = IntPtr.Zero;
                            uint flags = WindowManager.SWP_NOACTIVATE | WindowManager.SWP_NOOWNERZORDER | WindowManager.SWP_SHOWWINDOW;
                            
                            IntPtr windowAboveDiscord = WindowManager.GetWindow(discordHwnd, WindowManager.GW_HWNDPREV);
                            
                            if (windowAboveDiscord == viewerHwnd)
                            {
                                // We are exactly above discord, don't change Z order
                                flags |= WindowManager.SWP_NOZORDER;
                            }
                            else if (windowAboveDiscord != IntPtr.Zero)
                            {
                                // Insert us behind the window that is above discord
                                insertAfter = windowAboveDiscord;
                            }
                            else
                            {
                                // Discord is the top-most window, put us at the very top
                                insertAfter = IntPtr.Zero;
                            }

                            WindowManager.SetWindowPos(
                                viewerHwnd,
                                insertAfter,
                                d_x + config.OFFSET_X,
                                d_y + config.OFFSET_Y,
                                new_width,
                                new_height,
                                flags
                            );

                            lastX = d_x; lastY = d_y;
                            lastW = d_w; lastH = d_h;
                        }
                    }
                }
                else
                {
                    if (WindowManager.IsWindowVisible(viewerHwnd))
                        WindowManager.ShowWindow(viewerHwnd, WindowManager.SW_HIDE);
                }
            }
        }

        private void GlobalHotkey_HotkeyPressed(object sender, EventArgs e)
        {
            if (!config.ATTACH_TO_DISCORD) return;

            isVisible = !isVisible;
            if (overlayForm != null && !overlayForm.IsDisposed)
            {
                if (isVisible) WindowManager.ShowWindow(overlayForm.Handle, WindowManager.SW_SHOWNA);
                else WindowManager.ShowWindow(overlayForm.Handle, WindowManager.SW_HIDE);
            }
        }

        private void TriggerRestart(object sender, EventArgs e)
        {
            restartRequested = true;
        }
    }
}
