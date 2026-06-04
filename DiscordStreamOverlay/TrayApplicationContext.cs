using System;
using System.Drawing;
using System.Windows.Forms;

namespace DiscordStreamOverlay
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
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

        public TrayApplicationContext()
        {
            config = ConfigManager.Load();

            trayIcon = new NotifyIcon()
            {
                Icon = CreateTrayIcon(),
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "Discord Stream Overlay"
            };

            trayIcon.ContextMenuStrip.Items.Add("Close/Open Settings", null, ToggleOptions);
            trayIcon.ContextMenuStrip.Items.Add("Restart Stream", null, TriggerRestart);
            trayIcon.ContextMenuStrip.Items.Add("Quit", null, Exit);

            globalHotkey = new GlobalHotkey();
            globalHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_STREAM);
            globalHotkey.HotkeyPressed += GlobalHotkey_HotkeyPressed;

            StartStreamCycle();

            int maxHz = WindowManager.GetFastestMonitorRefreshRate();
            int intervalMs = Math.Max(1, 1000 / maxHz);

            loopTimer = new System.Windows.Forms.Timer { Interval = intervalMs }; // e.g., 6ms for 144Hz, 16ms for 60Hz
            loopTimer.Tick += LoopTimer_Tick;
            loopTimer.Start();
        }

        private Icon CreateTrayIcon()
        {
            Bitmap bmp = new Bitmap(64, 64);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(88, 101, 242));
                g.FillRectangle(Brushes.White, 16, 16, 32, 32);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void StartStreamCycle()
        {
            if (overlayForm != null)
            {
                overlayForm.Close();
                overlayForm.Dispose();
                overlayForm = null;
            }

            discordHwnd = WindowManager.FindDiscordWindow();
            if (discordHwnd == IntPtr.Zero)
            {
                return; // Will retry in the loop
            }

            if (WindowManager.IsIconic(discordHwnd) || !WindowManager.IsWindowVisible(discordHwnd))
            {
                // Discord is minimized or hidden. Do not create the form yet.
                // We leave overlayForm as null. The loop will create it when Discord is restored.
                return;
            }

            lastX = -1; lastY = -1; lastW = -1; lastH = -1;

            overlayForm = new OverlayForm(config.STREAM_URL);
            overlayForm.FullscreenStateChanged += OverlayForm_FullscreenStateChanged;
            
            // Set position immediately before showing to prevent visual glitches
            WindowManager.GetWindowRect(discordHwnd, out WindowManager.RECT rect);
            int d_x = rect.Left;
            int d_y = rect.Top;
            int d_w = rect.Right - rect.Left;
            int d_h = rect.Bottom - rect.Top;

            int new_width = d_w - config.OFFSET_X - config.MARGIN_RIGHT;
            int new_height = d_h - config.OFFSET_Y - config.MARGIN_BOTTOM;
            if (new_width < 100) new_width = 100;
            if (new_height < 100) new_height = 100;

            overlayForm.StartPosition = FormStartPosition.Manual;
            overlayForm.Location = new Point(d_x + config.OFFSET_X, d_y + config.OFFSET_Y);
            overlayForm.Size = new Size(new_width, new_height);

            // Show with Discord as owner
            overlayForm.Show(new WindowWrapper(discordHwnd));
        }

        private void OverlayForm_FullscreenStateChanged(object sender, EventArgs e)
        {
            if (overlayForm == null) return;
            IntPtr viewerHwnd = overlayForm.Handle;

            if (overlayForm.IsFullscreenMode)
            {
                // Detach from Discord
                WindowManager.SetWindowLongPtr(viewerHwnd, WindowManager.GWL_HWNDPARENT, IntPtr.Zero);

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
                WindowManager.SetWindowLongPtr(viewerHwnd, WindowManager.GWL_HWNDPARENT, discordHwnd);
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
                        lastX = d_x; lastY = d_y; lastW = d_w; lastH = d_h;

                        int new_width = d_w - config.OFFSET_X - config.MARGIN_RIGHT;
                        int new_height = d_h - config.OFFSET_Y - config.MARGIN_BOTTOM;

                        if (new_width < 100) new_width = 100;
                        if (new_height < 100) new_height = 100;

                        WindowManager.SetWindowPos(
                            viewerHwnd,
                            IntPtr.Zero,
                            d_x + config.OFFSET_X,
                            d_y + config.OFFSET_Y,
                            new_width,
                            new_height,
                            WindowManager.SWP_NOZORDER | WindowManager.SWP_NOACTIVATE | WindowManager.SWP_NOOWNERZORDER | WindowManager.SWP_SHOWWINDOW
                        );
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
            isVisible = !isVisible;
            if (overlayForm != null && !overlayForm.IsDisposed)
            {
                if (isVisible) WindowManager.ShowWindow(overlayForm.Handle, WindowManager.SW_SHOWNA);
                else WindowManager.ShowWindow(overlayForm.Handle, WindowManager.SW_HIDE);
            }
        }

        private void ToggleOptions(object sender, EventArgs e)
        {
            SettingsForm settings = new SettingsForm(config);
            settings.SettingsSaved += (s, ev) =>
            {
                config = ConfigManager.Load();
                globalHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_STREAM);
                TriggerRestart(null, null);
            };
            settings.ShowDialog();
        }

        private void TriggerRestart(object sender, EventArgs e)
        {
            restartRequested = true;
        }

        private void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            globalHotkey.Dispose();
            if (overlayForm != null) overlayForm.Close();
            Application.Exit();
        }
    }
}
