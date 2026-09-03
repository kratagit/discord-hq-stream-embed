using System;
using System.Drawing;
using System.Windows.Forms;

namespace WhipCast
{
    public class AppContext : ApplicationContext
    {
        private AppConfig config;
        private GlobalHotkey globalHotkey;
        private GlobalHotkey modeHotkey;
        private OverlayForm? overlayForm;
        private System.Windows.Forms.Timer loopTimer;

        private IntPtr targetHwnd = IntPtr.Zero;
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

            globalHotkey = new GlobalHotkey();
            globalHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_STREAM);
            globalHotkey.HotkeyPressed += GlobalHotkey_HotkeyPressed;

            modeHotkey = new GlobalHotkey();
            modeHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_MODE);
            modeHotkey.HotkeyPressed += ModeHotkey_HotkeyPressed;

            StartStreamCycle();

            int maxHz = WindowManager.GetFastestMonitorRefreshRate();
            int intervalMs = Math.Max(1, 1000 / maxHz);

            loopTimer = new System.Windows.Forms.Timer { Interval = intervalMs }; // e.g., 6ms for 144Hz, 16ms for 60Hz
            loopTimer.Tick += LoopTimer_Tick;
            loopTimer.Start();
        }

        private void StartStreamCycle()
        {
            if (overlayForm != null)
            {
                overlayForm.IsRestarting = true;
                overlayForm.Close();
                overlayForm.Dispose();
                overlayForm = null;
            }

            if (!config.ATTACH_TO_WINDOW)
            {
                Icon? standaloneIcon = null;
                try
                {
                    using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WhipCast.icon.ico"))
                    {
                        if (stream != null) standaloneIcon = new Icon(stream);
                    }
                }
                catch { }

                overlayForm = new OverlayForm(config, standaloneIcon);
                WireOverlayForm(overlayForm);
                overlayForm.Show();
                return;
            }

            targetHwnd = WindowManager.FindTargetWindow();
            if (targetHwnd == IntPtr.Zero)
            {
                return; // Will retry in the loop
            }

            if (WindowManager.IsIconic(targetHwnd) || !WindowManager.IsWindowVisible(targetHwnd))
            {
                // whip-cast is minimized or hidden. Do not create the form yet.
                // We leave overlayForm as null. The loop will create it when whip-cast is restored.
                return;
            }

            lastX = -1; lastY = -1; lastW = -1; lastH = -1;

            overlayForm = new OverlayForm(config);
            WireOverlayForm(overlayForm);

            // Set position immediately before showing to prevent visual glitches
            WindowManager.GetWindowRect(targetHwnd, out WindowManager.RECT rect);
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

            // Show with whip-cast as owner
            overlayForm.Show(new WindowWrapper(targetHwnd));
        }

        // Both run modes attach the same handlers; only form construction differs.
        // Takes the form as an argument rather than reading the field, so the caller's
        // freshly built instance is what gets wired and no null check is needed here.
        private void WireOverlayForm(OverlayForm form)
        {
            form.FullscreenStateChanged += OverlayForm_FullscreenStateChanged;
            form.RequestRestart += (s, e) => {
                config = ConfigManager.Load();
                globalHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_STREAM);
                modeHotkey.SetHotkeyString(config.HOTKEY_TOGGLE_MODE);
                TriggerRestart(null, null);
            };
            form.RequestExit += (s, e) => {
                globalHotkey?.Dispose();
                modeHotkey?.Dispose();
                loopTimer?.Stop();
                if (overlayForm != null && !overlayForm.IsDisposed) {
                    try { overlayForm.Close(); } catch { }
                }
                ExitThread();
            };
        }

        private FormBorderStyle previousBorderStyle = FormBorderStyle.Sizable;
        private FormWindowState previousWindowState = FormWindowState.Normal;

        private void OverlayForm_FullscreenStateChanged(object? sender, EventArgs e)
        {
            if (overlayForm == null) return;
            IntPtr viewerHwnd = overlayForm.Handle;

            if (!config.ATTACH_TO_WINDOW)
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
                // Detach from whip-cast
                WindowManager.SetWindowLongPtr(viewerHwnd, WindowManager.GWL_HWNDPARENT, IntPtr.Zero);

                // Find nearest monitor to whip-cast
                IntPtr monitor = WindowManager.MonitorFromWindow(targetHwnd, WindowManager.MONITOR_DEFAULTTONEAREST);
                WindowManager.MONITORINFO monitorInfo = new WindowManager.MONITORINFO();
                monitorInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(WindowManager.MONITORINFO));

                int mx = 0, my = 0, mw = 0, mh = 0;
                if (WindowManager.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    mx = monitorInfo.rcMonitor.Left;
                    my = monitorInfo.rcMonitor.Top;
                    mw = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                    mh = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                }
                else if (Screen.PrimaryScreen is Screen primary)
                {
                    mw = primary.Bounds.Width;
                    mh = primary.Bounds.Height;
                }

                // With neither a monitor rect nor a primary screen there is no sane size to
                // use, and resizing to 0x0 would be worse than leaving the window alone.
                if (mw > 0 && mh > 0)
                {
                    WindowManager.SetWindowPos(viewerHwnd, WindowManager.HWND_TOPMOST, mx, my, mw, mh, WindowManager.SWP_SHOWWINDOW);
                }
            }
            else
            {
                // Reattach
                WindowManager.SetWindowLongPtr(viewerHwnd, WindowManager.GWL_HWNDPARENT, targetHwnd);
                WindowManager.SetWindowPos(viewerHwnd, WindowManager.HWND_NOTOPMOST, 0, 0, 0, 0, WindowManager.SWP_NOMOVE | WindowManager.SWP_NOSIZE);

                // Leaving fullscreen only restores the owner and z-order; size and
                // position are the timer's job. But the timer repositions solely when
                // Discord's rect changes, and Discord did not move while we were
                // fullscreen, so its dirty-check would skip us and the window would stay
                // stretched across the monitor. Invalidate the cache to force one update.
                lastX = -1; lastY = -1; lastW = -1; lastH = -1;
            }
        }

        private void LoopTimer_Tick(object? sender, EventArgs e)
        {
            if (restartRequested)
            {
                restartRequested = false;
                StartStreamCycle();
                return;
            }

            if (!config.ATTACH_TO_WINDOW)
            {
                return; // In standalone mode, do not track whip-cast
            }

            if (!WindowManager.IsWindow(targetHwnd))
            {
                // whip-cast closed, find again
                targetHwnd = WindowManager.FindTargetWindow();
                if (targetHwnd == IntPtr.Zero && overlayForm != null)
                {
                    overlayForm.Close();
                    overlayForm = null;
                }
                return;
            }

            // If whip-cast is valid but overlay is missing, try to create it IF whip-cast is ready
            if (overlayForm == null || overlayForm.IsDisposed)
            {
                if (!WindowManager.IsIconic(targetHwnd) && WindowManager.IsWindowVisible(targetHwnd))
                {
                    StartStreamCycle();
                }
                return; // Wait until created
            }

            if (overlayForm.IsFullscreenMode) return; // Skip resizing if fullscreen

            IntPtr viewerHwnd = overlayForm.Handle;

            if (WindowManager.IsIconic(targetHwnd))
            {
                // When whip-cast is minimized to taskbar, the OS automatically hides owned windows.
                // We do NOT explicitly call SW_HIDE because it breaks the OS restore logic.
            }
            else if (!WindowManager.IsWindowVisible(targetHwnd))
            {
                // whip-cast is hidden to the system tray (not minimized). The OS doesn't hide the child.
                // We must hide it manually.
                if (WindowManager.IsWindowVisible(viewerHwnd))
                {
                    WindowManager.ShowWindow(viewerHwnd, WindowManager.SW_HIDE);
                }

                // Invalidate cache so when whip-cast comes back, it forces SWP_SHOWWINDOW
                lastX = -1; lastY = -1; lastW = -1; lastH = -1;
            }
            else
            {
                if (isVisible)
                {
                    WindowManager.GetWindowRect(targetHwnd, out WindowManager.RECT rect);
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

        private void GlobalHotkey_HotkeyPressed(object? sender, EventArgs e)
        {
            isVisible = !isVisible;
            if (overlayForm != null && !overlayForm.IsDisposed)
            {
                if (isVisible) WindowManager.ShowWindow(overlayForm.Handle, WindowManager.SW_SHOWNA);
                else WindowManager.ShowWindow(overlayForm.Handle, WindowManager.SW_HIDE);
            }
            else if (!config.ATTACH_TO_WINDOW && isVisible)
            {
                StartStreamCycle();
            }
        }

        private void ModeHotkey_HotkeyPressed(object? sender, EventArgs e)
        {
            config.ATTACH_TO_WINDOW = !config.ATTACH_TO_WINDOW;
            ConfigManager.Save(config);

            // Immediately restart the stream with new mode
            TriggerRestart(null, null);
        }

        private void TriggerRestart(object? sender, EventArgs? e)
        {
            restartRequested = true;
        }
    }
}
